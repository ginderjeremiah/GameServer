import { describe, it, expect, vi, beforeEach, type Mock } from 'vitest';

interface MockXhr {
	open: Mock;
	send: Mock;
	setRequestHeader: Mock;
	status: number;
	statusText: string;
	responseText: string;
	onload: (() => void) | null;
	onerror: (() => void) | null;
	onabort: (() => void) | null;
}

let xhrInstances: MockXhr[] = [];

function createMockXhr(): MockXhr {
	const xhr: MockXhr = {
		open: vi.fn(),
		send: vi.fn(),
		setRequestHeader: vi.fn(),
		status: 0,
		statusText: 'OK',
		responseText: '',
		onload: null,
		onerror: null,
		onabort: null
	};
	// Default behaviour: a successful response that synchronously fires the onload handler. Tests that
	// need other status codes (e.g. a 401 then a retry) install an `xhrResponder` to drive each call.
	xhr.send.mockImplementation(() => {
		if (xhrResponder) {
			xhrResponder(xhr);
		} else {
			xhr.status = 200;
			xhr.responseText = JSON.stringify({ data: 'test-result' });
		}
		xhr.onload?.();
	});
	xhrInstances.push(xhr);
	return xhr;
}

let xhrResponder: ((xhr: MockXhr) => void) | null = null;

// Returning an object from a constructor makes `new XMLHttpRequest()` resolve to that object.
vi.stubGlobal('XMLHttpRequest', vi.fn(createMockXhr));
vi.stubGlobal('window', { encodeURIComponent });

import { ApiRequest } from '$lib/api/api-request';
import { setTokens } from '$lib/api/token-store';

// Builds a JWT-shaped access token whose `exp` claim is `secondsFromNow` in the future, so the
// request layer treats it as valid and attaches it without attempting a pre-emptive refresh.
function makeAccessToken(secondsFromNow: number): string {
	const exp = Math.floor(Date.now() / 1000) + secondsFromNow;
	const payload = btoa(JSON.stringify({ exp })).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
	return `header.${payload}.signature`;
}

const authHeader = (xhr: MockXhr): string | undefined =>
	xhr.setRequestHeader.mock.calls.find((call) => call[0] === 'Authorization')?.[1];

describe('ApiRequest', () => {
	beforeEach(() => {
		xhrInstances = [];
		xhrResponder = null;
		localStorage.clear();
	});

	const lastXhr = () => xhrInstances[xhrInstances.length - 1];

	describe('get (instance)', () => {
		it('opens a GET request to /api/{endpoint}', async () => {
			await new ApiRequest('Items').get();

			expect(lastXhr().open).toHaveBeenCalledWith('GET', '/api/Items', true);
		});

		it('appends URL params when provided', async () => {
			await new ApiRequest('Items/SlotsForItem').get({ itemId: 5, refreshCache: true });

			const url = lastXhr().open.mock.calls[0][1] as string;
			expect(url).toContain('itemId=5');
			expect(url).toContain('refreshCache=true');
		});

		it('attaches the bearer access token when one is stored', async () => {
			setTokens({ accessToken: makeAccessToken(600), refreshToken: 'refresh' });

			await new ApiRequest('Items').get();

			expect(authHeader(lastXhr())).toMatch(/^Bearer header\./);
		});

		it('sends no Authorization header when not logged in', async () => {
			await new ApiRequest('Items').get();

			expect(authHeader(lastXhr())).toBeUndefined();
		});

		it('returns an ApiResponse with parsed data', async () => {
			const response = await new ApiRequest('Items').get();

			const data: unknown = response.data;
			expect(data).toBe('test-result');
		});

		it('resolves even when send throws', async () => {
			xhrResponder = () => {
				throw new Error('Network error');
			};

			const response = await new ApiRequest('Items').get();
			expect(response).toBeDefined();
		});

		it('skips undefined URL params', async () => {
			await new ApiRequest('Items/SlotsForItem').get({ itemId: 5, refreshCache: undefined });

			const url = lastXhr().open.mock.calls[0][1] as string;
			expect(url).toContain('itemId=5');
			expect(url).not.toContain('refreshCache');
		});
	});

	describe('post', () => {
		it('opens a POST request to /api/{endpoint}', async () => {
			await new ApiRequest('Login').post({ username: 'u', password: 'p' });

			expect(lastXhr().open).toHaveBeenCalledWith('POST', '/api/Login', true);
		});

		it('sets content-type to application/json', async () => {
			await new ApiRequest('Login').post({ username: 'u', password: 'p' });

			expect(lastXhr().setRequestHeader).toHaveBeenCalledWith('content-type', 'application/json');
		});

		it('sends JSON-stringified payload', async () => {
			const payload = { username: 'user', password: 'pass' };
			await new ApiRequest('Login').post(payload);

			expect(lastXhr().send).toHaveBeenCalledWith(JSON.stringify(payload));
		});

		it('resolves even when send throws', async () => {
			xhrResponder = () => {
				throw new Error('Network error');
			};

			const response = await new ApiRequest('Login').post({ username: 'u', password: 'p' });
			expect(response).toBeDefined();
		});

		it('sends an empty body for endpoints with no request payload', async () => {
			await new ApiRequest('Login/Logout').post();

			expect(lastXhr().open).toHaveBeenCalledWith('POST', '/api/Login/Logout', true);
			expect(lastXhr().send).toHaveBeenCalledWith(undefined);
		});
	});

	describe('static get', () => {
		it('creates a request and returns data directly', async () => {
			const data: unknown = await ApiRequest.get('Items');
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

			const fetchMock = vi.fn(async () => ({
				ok: true,
				json: async () => ({ data: { accessToken: makeAccessToken(900), refreshToken: 'refresh-2' } })
			}));
			vi.stubGlobal('fetch', fetchMock);

			let calls = 0;
			xhrResponder = (xhr) => {
				calls += 1;
				if (calls === 1) {
					xhr.status = 401;
					xhr.responseText = JSON.stringify({ errorMessage: 'expired' });
				} else {
					xhr.status = 200;
					xhr.responseText = JSON.stringify({ data: 'retried' });
				}
			};

			const response = await new ApiRequest('Items').get();

			expect(fetchMock).toHaveBeenCalledTimes(1);
			expect(xhrInstances).toHaveLength(2);
			expect(response.status).toBe(200);
			expect(response.data as unknown).toBe('retried');
			// The retry carries the freshly rotated access token, not the one that was rejected.
			expect(authHeader(xhrInstances[1])).not.toBe(authHeader(xhrInstances[0]));
		});

		it('does not refresh anonymous endpoints', async () => {
			setTokens({ accessToken: makeAccessToken(600), refreshToken: 'refresh-1' });

			const fetchMock = vi.fn();
			vi.stubGlobal('fetch', fetchMock);

			xhrResponder = (xhr) => {
				xhr.status = 401;
				xhr.responseText = JSON.stringify({ errorMessage: 'nope' });
			};

			const response = await new ApiRequest('Login').post({ username: 'u', password: 'p' });

			expect(fetchMock).not.toHaveBeenCalled();
			expect(xhrInstances).toHaveLength(1);
			expect(response.status).toBe(401);
		});
	});
});
