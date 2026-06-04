import { describe, it, expect, vi, beforeEach, type Mock } from 'vitest';

interface MockXhr {
	open: Mock;
	send: Mock;
	setRequestHeader: Mock;
	withCredentials: boolean;
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
		withCredentials: false,
		status: 0,
		statusText: 'OK',
		responseText: '',
		onload: null,
		onerror: null,
		onabort: null
	};
	// Default behaviour: a successful response that synchronously fires the onload handler.
	xhr.send.mockImplementation(() => {
		xhr.status = 200;
		xhr.responseText = JSON.stringify({ data: 'test-result' });
		xhr.onload?.();
	});
	xhrInstances.push(xhr);
	return xhr;
}

// Returning an object from a constructor makes `new XMLHttpRequest()` resolve to that object.
vi.stubGlobal('XMLHttpRequest', vi.fn(createMockXhr));
vi.stubGlobal('window', { encodeURIComponent });

import { ApiRequest } from '$lib/api/api-request';

describe('ApiRequest', () => {
	beforeEach(() => {
		xhrInstances = [];
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

		it('sets withCredentials to true', async () => {
			await new ApiRequest('Items').get();

			expect(lastXhr().withCredentials).toBe(true);
		});

		it('returns an ApiResponse with parsed data', async () => {
			const response = await new ApiRequest('Items').get();

			const data: unknown = response.data;
			expect(data).toBe('test-result');
		});

		it('resolves even when send throws', async () => {
			const request = new ApiRequest('Items');
			lastXhr().send.mockImplementation(() => {
				throw new Error('Network error');
			});

			const response = await request.get();
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
			const request = new ApiRequest('Login');
			lastXhr().send.mockImplementation(() => {
				throw new Error('Network error');
			});

			const response = await request.post({ username: 'u', password: 'p' });
			expect(response).toBeDefined();
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
});
