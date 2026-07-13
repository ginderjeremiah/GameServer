import { describe, it, expect, vi, beforeEach } from 'vitest';

interface FetchCall {
	url: string;
	method: string;
	headers: Record<string, string>;
	body: string | undefined;
}

interface StubResponse {
	status: number;
	statusText?: string;
	body: string;
}

let fetchCalls: FetchCall[] = [];

// Per-test hook to drive each `fetch` call. Tests that need specific status codes (e.g. a 401 then a
// retry, or a token refresh) install a responder; otherwise a successful 200 is returned.
let fetchResponder: ((call: FetchCall) => StubResponse) | null = null;

// A minimal stand-in for the parts of the `fetch` Response the API client touches (`status`,
// `statusText`, `text()`).
const makeResponse = ({ status, statusText, body }: StubResponse): Response =>
	({
		status,
		statusText: statusText ?? 'OK',
		ok: status >= 200 && status < 300,
		text: async () => body,
		json: async () => JSON.parse(body)
	}) as Response;

const fetchMock = vi.fn(async (url: string, init?: RequestInit) => {
	const call: FetchCall = {
		url,
		method: init?.method ?? 'GET',
		headers: (init?.headers as Record<string, string>) ?? {},
		body: init?.body as string | undefined
	};
	fetchCalls.push(call);

	const stub = fetchResponder?.(call) ?? { status: 200, body: JSON.stringify({ data: 'test-result' }) };
	return makeResponse(stub);
});

vi.stubGlobal('fetch', fetchMock);
// `location` is a minimal stand-in so handleAuthFailure's redirect guard (window.location.pathname) can
// run without throwing; only the refresh-failure tests below inspect it.
vi.stubGlobal('window', { location: { href: '', pathname: '/game' } });

// The "refresh definitively failed" tests below drive the real refreshTokens/handleAuthFailure so the
// 401-retry and redirect wiring is exercised end to end; the "recovers from a concurrent tab" test
// needs refreshTokens to resolve a rejection on demand (independent of the fetch mock's timing) to pin
// the caller-side re-read guard in isolation, so refreshTokens is wrapped rather than driven purely by fetch.
vi.mock('$lib/api/auth', async (importOriginal) => {
	const actual = await importOriginal<typeof import('$lib/api/auth')>();
	return { ...actual, refreshTokens: vi.fn(actual.refreshTokens) };
});

import { ApiRequest } from '$lib/api/api-request';
import { setTokens, getTokens } from '$lib/api/token-store';
import { refreshTokens } from '$lib/api/auth';

// Builds a JWT-shaped access token whose `exp` claim is `secondsFromNow` in the future, so the
// request layer treats it as valid and attaches it without attempting a pre-emptive refresh.
function makeAccessToken(secondsFromNow: number): string {
	const exp = Math.floor(Date.now() / 1000) + secondsFromNow;
	const payload = btoa(JSON.stringify({ exp })).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
	return `header.${payload}.signature`;
}

const authHeader = (call: FetchCall): string | undefined => call.headers['Authorization'];
const callsTo = (url: string): FetchCall[] => fetchCalls.filter((call) => call.url === url);

describe('ApiRequest', () => {
	beforeEach(() => {
		fetchCalls = [];
		fetchResponder = null;
		localStorage.clear();
		window.location.href = '';
		vi.mocked(refreshTokens).mockClear();
	});

	const lastCall = () => fetchCalls[fetchCalls.length - 1];

	describe('get (instance)', () => {
		it('sends a GET request to /api/{endpoint}', async () => {
			await new ApiRequest('Tags').get();

			expect(lastCall().method).toBe('GET');
			expect(lastCall().url).toBe('/api/Tags');
		});

		it('appends URL params when provided', async () => {
			await new ApiRequest('AdminTools/GetUsers').get({ page: 2, pageSize: 25 });

			expect(lastCall().url).toContain('page=2');
			expect(lastCall().url).toContain('pageSize=25');
		});

		it('attaches the bearer access token when one is stored', async () => {
			setTokens({ accessToken: makeAccessToken(600), refreshToken: 'refresh' });

			await new ApiRequest('Tags').get();

			expect(authHeader(lastCall())).toMatch(/^Bearer header\./);
		});

		it('sends no Authorization header when not logged in', async () => {
			await new ApiRequest('Tags').get();

			expect(authHeader(lastCall())).toBeUndefined();
		});

		it('returns an ApiResponse with parsed data', async () => {
			const response = await new ApiRequest('Tags').get();

			const data: unknown = response.data;
			expect(data).toBe('test-result');
		});

		it('resolves even when the request fails', async () => {
			fetchResponder = () => {
				throw new Error('Network error');
			};

			const response = await new ApiRequest('Tags').get();
			expect(response).toBeDefined();
			expect(response.status).toBe(0);
		});

		it('skips undefined URL params', async () => {
			await new ApiRequest('AdminTools/GetUsers').get({ page: 5, archived: undefined });

			expect(lastCall().url).toContain('page=5');
			expect(lastCall().url).not.toContain('archived');
		});
	});

	describe('post', () => {
		it('sends a POST request to /api/{endpoint}', async () => {
			await new ApiRequest('Login').post({ username: 'u', password: 'p' });

			expect(lastCall().method).toBe('POST');
			expect(lastCall().url).toBe('/api/Login');
		});

		it('sets content-type to application/json', async () => {
			await new ApiRequest('Login').post({ username: 'u', password: 'p' });

			expect(lastCall().headers['content-type']).toBe('application/json');
		});

		it('sends JSON-stringified payload', async () => {
			const payload = { username: 'user', password: 'pass' };
			await new ApiRequest('Login').post(payload);

			expect(lastCall().body).toBe(JSON.stringify(payload));
		});

		it('resolves even when the request fails', async () => {
			fetchResponder = () => {
				throw new Error('Network error');
			};

			const response = await new ApiRequest('Login').post({ username: 'u', password: 'p' });
			expect(response).toBeDefined();
			expect(response.status).toBe(0);
		});

		it('sends an empty body for endpoints with no request payload', async () => {
			await new ApiRequest('Login/Logout').post();

			expect(lastCall().method).toBe('POST');
			expect(lastCall().url).toBe('/api/Login/Logout');
			expect(lastCall().body).toBeUndefined();
		});
	});

	describe('static get', () => {
		it('creates a request and returns data directly', async () => {
			const data: unknown = await ApiRequest.get('Tags');
			expect(data).toBe('test-result');
		});
	});

	describe('static post', () => {
		it('creates a request and returns data directly', async () => {
			const data: unknown = await ApiRequest.post('Login', { username: 'u', password: 'p' });
			expect(data).toBe('test-result');
		});
	});

	describe('silent refresh on 401', () => {
		it('refreshes the token pair and retries the request once', async () => {
			setTokens({ accessToken: makeAccessToken(600), refreshToken: 'refresh-1' });

			let itemsCalls = 0;
			fetchResponder = (call) => {
				if (call.url === '/api/Login/Refresh') {
					return {
						status: 200,
						body: JSON.stringify({ data: { accessToken: makeAccessToken(900), refreshToken: 'refresh-2' } })
					};
				}
				itemsCalls += 1;
				return itemsCalls === 1
					? { status: 401, body: JSON.stringify({ errorMessage: 'expired' }) }
					: { status: 200, body: JSON.stringify({ data: 'retried' }) };
			};

			const response = await new ApiRequest('Tags').get();

			expect(callsTo('/api/Login/Refresh')).toHaveLength(1);
			const itemsRequests = callsTo('/api/Tags');
			expect(itemsRequests).toHaveLength(2);
			expect(response.status).toBe(200);
			expect(response.data as unknown).toBe('retried');
			// The retry carries the freshly rotated access token, not the one that was rejected.
			expect(authHeader(itemsRequests[1])).not.toBe(authHeader(itemsRequests[0]));
		});

		it('does not refresh anonymous endpoints', async () => {
			setTokens({ accessToken: makeAccessToken(600), refreshToken: 'refresh-1' });

			fetchResponder = () => ({ status: 401, body: JSON.stringify({ errorMessage: 'nope' }) });

			const response = await new ApiRequest('Login').post({ username: 'u', password: 'p' });

			expect(callsTo('/api/Login/Refresh')).toHaveLength(0);
			expect(fetchCalls).toHaveLength(1);
			expect(response.status).toBe(401);
		});

		it('clears tokens and redirects when the refresh is definitively rejected (400)', async () => {
			setTokens({ accessToken: makeAccessToken(600), refreshToken: 'refresh-1' });
			window.location.href = '';

			fetchResponder = (call) =>
				call.url === '/api/Login/Refresh'
					? { status: 400, body: JSON.stringify({ errorMessage: 'Invalid or expired refresh token' }) }
					: { status: 401, body: JSON.stringify({ errorMessage: 'expired' }) };

			const response = await new ApiRequest('Tags').get();

			// The original 401 is what the caller sees; only one refresh attempt is made (the isRetry guard
			// prevents `execute` from recursing into a second 401-triggered refresh on the retried request).
			expect(response.status).toBe(401);
			expect(callsTo('/api/Login/Refresh')).toHaveLength(1);
			expect(callsTo('/api/Tags')).toHaveLength(1);
			expect(getTokens()).toBeNull();
			expect(window.location.href).toBe('/');
		});

		it('preserves tokens and does not redirect when the refresh is retryable (5xx)', async () => {
			const accessToken = makeAccessToken(600);
			setTokens({ accessToken, refreshToken: 'refresh-1' });
			window.location.href = '';

			fetchResponder = (call) =>
				call.url === '/api/Login/Refresh'
					? { status: 503, body: '' }
					: { status: 401, body: JSON.stringify({ errorMessage: 'expired' }) };

			const response = await new ApiRequest('Tags').get();

			// A transient refresh failure just surfaces the original 401 — no forced logout, and the isRetry
			// guard still caps this at a single refresh attempt rather than looping.
			expect(response.status).toBe(401);
			expect(callsTo('/api/Login/Refresh')).toHaveLength(1);
			expect(callsTo('/api/Tags')).toHaveLength(1);
			expect(getTokens()).toEqual({ accessToken, refreshToken: 'refresh-1' });
			expect(window.location.href).toBe('');
		});

		it("recovers using a concurrent tab's rotated pair instead of clearing, when one lands after refreshTokens gives up", async () => {
			setTokens({ accessToken: makeAccessToken(600), refreshToken: 'refresh-1' });
			// Simulate the exact residual race: refreshTokens() itself has already concluded the presented
			// token is dead (a mocked rejection, bypassing its own internal re-read), but by the time the
			// caller re-checks storage, another tab's rotated pair has since landed.
			vi.mocked(refreshTokens).mockResolvedValueOnce({ status: 'rejected' });
			setTokens({ accessToken: makeAccessToken(900), refreshToken: 'winner-refresh' });

			let itemsCalls = 0;
			fetchResponder = () => {
				itemsCalls += 1;
				return itemsCalls === 1
					? { status: 401, body: JSON.stringify({ errorMessage: 'expired' }) }
					: { status: 200, body: JSON.stringify({ data: 'retried' }) };
			};

			const response = await new ApiRequest('Tags').get();

			expect(callsTo('/api/Login/Refresh')).toHaveLength(0);
			expect(response.status).toBe(200);
			expect(response.data as unknown).toBe('retried');
			// The winner's pair is left intact — never cleared.
			expect(getTokens()).toEqual({ accessToken: expect.any(String), refreshToken: 'winner-refresh' });
			expect(window.location.href).toBe('');
		});
	});
});
