import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { ensureValidAccessToken, handleAuthFailure, refreshTokens } from '$lib/api/auth';
import { getTokens, setTokens } from '$lib/api/token-store';

function makeAccessToken(secondsFromNow: number): string {
	const exp = Math.floor(Date.now() / 1000) + secondsFromNow;
	const payload = btoa(JSON.stringify({ exp })).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
	return `header.${payload}.signature`;
}

const okResponse = (accessToken: string, refreshToken: string) => ({
	ok: true,
	json: async () => ({ data: { accessToken, refreshToken } })
});

describe('auth', () => {
	beforeEach(() => {
		localStorage.clear();
		vi.restoreAllMocks();
	});

	afterEach(() => {
		vi.unstubAllGlobals();
	});

	describe('refreshTokens', () => {
		it('returns rejected and makes no request when there is no refresh token', async () => {
			const fetchMock = vi.fn();
			vi.stubGlobal('fetch', fetchMock);

			const result = await refreshTokens();

			expect(result).toEqual({ status: 'rejected' });
			expect(fetchMock).not.toHaveBeenCalled();
		});

		it('stores and returns the rotated pair on success', async () => {
			setTokens({ accessToken: 'old', refreshToken: 'old-refresh' });
			const fetchMock = vi.fn(async () => okResponse('new-access', 'new-refresh'));
			vi.stubGlobal('fetch', fetchMock);

			const result = await refreshTokens();

			expect(result).toEqual({
				status: 'success',
				tokens: { accessToken: 'new-access', refreshToken: 'new-refresh' }
			});
			expect(getTokens()).toEqual({ accessToken: 'new-access', refreshToken: 'new-refresh' });
			expect(fetchMock).toHaveBeenCalledWith(
				'/api/Login/Refresh',
				expect.objectContaining({ method: 'POST', body: JSON.stringify({ refreshToken: 'old-refresh' }) })
			);
		});

		it('clears storage and returns rejected when the server rejects the refresh token (400)', async () => {
			setTokens({ accessToken: 'old', refreshToken: 'old-refresh' });
			vi.stubGlobal(
				'fetch',
				vi.fn(async () => ({ ok: false, status: 400, json: async () => ({}) }))
			);

			const result = await refreshTokens();

			expect(result).toEqual({ status: 'rejected' });
			expect(getTokens()).toBeNull();
		});

		it("adopts another tab's rotated pair instead of clearing when storage no longer holds the presented token", async () => {
			setTokens({ accessToken: 'old', refreshToken: 'old-refresh' });
			vi.stubGlobal(
				'fetch',
				vi.fn(async () => {
					// Simulate another tab winning the race and rotating storage while this tab's
					// (now stale) refresh request is in flight — the backend 400s the spent token.
					setTokens({ accessToken: 'winner-access', refreshToken: 'winner-refresh' });
					return { ok: false, status: 400, json: async () => ({}) };
				})
			);

			const result = await refreshTokens();

			expect(result).toEqual({
				status: 'success',
				tokens: { accessToken: 'winner-access', refreshToken: 'winner-refresh' }
			});
			expect(getTokens()).toEqual({ accessToken: 'winner-access', refreshToken: 'winner-refresh' });
		});

		it('keeps storage and returns retryable on a network error (fetch throws)', async () => {
			setTokens({ accessToken: 'old', refreshToken: 'old-refresh' });
			vi.stubGlobal(
				'fetch',
				vi.fn(async () => {
					throw new TypeError('Failed to fetch');
				})
			);

			const result = await refreshTokens();

			expect(result).toEqual({ status: 'retryable' });
			expect(getTokens()).toEqual({ accessToken: 'old', refreshToken: 'old-refresh' });
		});

		it('keeps storage and returns retryable on a transient server error (5xx)', async () => {
			setTokens({ accessToken: 'old', refreshToken: 'old-refresh' });
			vi.stubGlobal(
				'fetch',
				vi.fn(async () => ({ ok: false, status: 503, json: async () => ({}) }))
			);

			const result = await refreshTokens();

			expect(result).toEqual({ status: 'retryable' });
			expect(getTokens()).toEqual({ accessToken: 'old', refreshToken: 'old-refresh' });
		});

		it('keeps storage and returns retryable when rate-limited (429)', async () => {
			setTokens({ accessToken: 'old', refreshToken: 'old-refresh' });
			vi.stubGlobal(
				'fetch',
				vi.fn(async () => ({ ok: false, status: 429, json: async () => ({}) }))
			);

			const result = await refreshTokens();

			expect(result).toEqual({ status: 'retryable' });
			expect(getTokens()).toEqual({ accessToken: 'old', refreshToken: 'old-refresh' });
		});

		it('keeps storage and returns retryable when a 2xx body fails to parse', async () => {
			setTokens({ accessToken: 'old', refreshToken: 'old-refresh' });
			vi.stubGlobal(
				'fetch',
				vi.fn(async () => ({
					ok: true,
					status: 200,
					json: async () => {
						// A proxy/captive portal can intercept with an HTML body that isn't JSON.
						throw new SyntaxError('Unexpected token < in JSON');
					}
				}))
			);

			const result = await refreshTokens();

			expect(result).toEqual({ status: 'retryable' });
			expect(getTokens()).toEqual({ accessToken: 'old', refreshToken: 'old-refresh' });
		});

		it('collapses concurrent refreshes onto a single request (single-use token safe)', async () => {
			setTokens({ accessToken: 'old', refreshToken: 'old-refresh' });
			let resolveFetch: (value: unknown) => void = () => {};
			const fetchMock = vi.fn(
				() =>
					new Promise((resolve) => {
						resolveFetch = resolve;
					})
			);
			vi.stubGlobal('fetch', fetchMock);

			const first = refreshTokens();
			const second = refreshTokens();

			resolveFetch(okResponse('new-access', 'new-refresh'));
			const [a, b] = await Promise.all([first, second]);

			expect(fetchMock).toHaveBeenCalledTimes(1);
			expect(a).toEqual({ status: 'success', tokens: { accessToken: 'new-access', refreshToken: 'new-refresh' } });
			expect(b).toEqual(a);
		});
	});

	describe('ensureValidAccessToken', () => {
		it('returns the current token without refreshing when it is still valid', async () => {
			const token = makeAccessToken(600);
			setTokens({ accessToken: token, refreshToken: 'r' });
			const fetchMock = vi.fn();
			vi.stubGlobal('fetch', fetchMock);

			const result = await ensureValidAccessToken();

			expect(result).toEqual({ accessToken: token, rejected: false });
			expect(fetchMock).not.toHaveBeenCalled();
		});

		it('refreshes when the access token is within the expiry leeway', async () => {
			setTokens({ accessToken: makeAccessToken(10), refreshToken: 'r' });
			const fetchMock = vi.fn(async () => okResponse('fresh-access', 'fresh-refresh'));
			vi.stubGlobal('fetch', fetchMock);

			const result = await ensureValidAccessToken();

			expect(result).toEqual({ accessToken: 'fresh-access', rejected: false });
			expect(fetchMock).toHaveBeenCalledTimes(1);
		});

		it('refreshes when the access token expiry cannot be determined', async () => {
			setTokens({ accessToken: 'opaque', refreshToken: 'r' });
			const fetchMock = vi.fn(async () => okResponse('fresh-access', 'fresh-refresh'));
			vi.stubGlobal('fetch', fetchMock);

			const result = await ensureValidAccessToken();

			expect(result).toEqual({ accessToken: 'fresh-access', rejected: false });
		});

		it('reports a definitive rejection without a token when the refresh token is dead', async () => {
			setTokens({ accessToken: makeAccessToken(10), refreshToken: 'r' });
			vi.stubGlobal(
				'fetch',
				vi.fn(async () => ({ ok: false, status: 400, json: async () => ({}) }))
			);

			const result = await ensureValidAccessToken();

			expect(result).toEqual({ accessToken: null, rejected: true });
		});

		it('reports retryable (not rejected) without a token on a network error', async () => {
			setTokens({ accessToken: makeAccessToken(10), refreshToken: 'r' });
			vi.stubGlobal(
				'fetch',
				vi.fn(async () => {
					throw new TypeError('Failed to fetch');
				})
			);

			const result = await ensureValidAccessToken();

			expect(result).toEqual({ accessToken: null, rejected: false });
		});
	});

	describe('handleAuthFailure', () => {
		let originalLocation: Location;

		beforeEach(() => {
			originalLocation = window.location;
		});

		afterEach(() => {
			Object.defineProperty(window, 'location', {
				configurable: true,
				writable: true,
				value: originalLocation
			});
		});

		const stubLocation = (pathname: string) => {
			Object.defineProperty(window, 'location', {
				configurable: true,
				writable: true,
				value: { href: '', pathname }
			});
		};

		it('clears tokens and redirects to login from a game page', () => {
			setTokens({ accessToken: 'a', refreshToken: 'r' });
			stubLocation('/game');

			handleAuthFailure();

			expect(getTokens()).toBeNull();
			expect(window.location.href).toBe('/');
		});

		it('clears tokens but does not redirect when already on the login page', () => {
			setTokens({ accessToken: 'a', refreshToken: 'r' });
			stubLocation('/');

			handleAuthFailure();

			expect(getTokens()).toBeNull();
			expect(window.location.href).toBe('');
		});
	});
});
