import { describe, it, expect, vi, beforeEach } from 'vitest';

let xhrInstances: any[];

vi.stubGlobal('XMLHttpRequest', function (this: any) {
	this.open = vi.fn();
	this.send = vi.fn(
		function (this: any) {
			this.status = 200;
			this.responseText = JSON.stringify({ data: 'test-result' });
			if (this.onload) this.onload();
		}.bind(this)
	);
	this.setRequestHeader = vi.fn();
	this.withCredentials = false;
	this.status = 0;
	this.statusText = 'OK';
	this.responseText = '';
	this.onload = null;
	this.onerror = null;
	this.onabort = null;
	xhrInstances.push(this);
});

vi.stubGlobal('window', { encodeURIComponent: encodeURIComponent });

import { ApiRequest } from '$lib/api/api-request';

describe('ApiRequest', () => {
	beforeEach(() => {
		xhrInstances = [];
	});

	function lastXhr() {
		return xhrInstances[xhrInstances.length - 1];
	}

	describe('get (instance)', () => {
		it('opens a GET request to /api/{endpoint}', async () => {
			const request = new ApiRequest('Items' as any);
			await request.get();

			expect(lastXhr().open).toHaveBeenCalledWith('GET', '/api/Items', true);
		});

		it('appends URL params when provided', async () => {
			const request = new ApiRequest('Items' as any);
			await (request as any).get({ id: 5, name: 'sword' });

			const url = lastXhr().open.mock.calls[0][1] as string;
			expect(url).toContain('id=5');
			expect(url).toContain('name=sword');
		});

		it('sets withCredentials to true', async () => {
			const request = new ApiRequest('Items' as any);
			await request.get();

			expect(lastXhr().withCredentials).toBe(true);
		});

		it('returns an ApiResponse with parsed data', async () => {
			const request = new ApiRequest('Items' as any);
			const response = await request.get();

			expect(response.data).toBe('test-result');
		});

		it('resolves even when send throws', async () => {
			const request = new ApiRequest('Items' as any);
			const xhr = lastXhr();
			xhr.send = vi.fn(() => {
				throw new Error('Network error');
			});

			const response = await request.get();
			expect(response).toBeDefined();
		});

		it('skips undefined URL params', async () => {
			const request = new ApiRequest('Items' as any);
			await (request as any).get({ id: 5, missing: undefined });

			const url = lastXhr().open.mock.calls[0][1] as string;
			expect(url).toContain('id=5');
			expect(url).not.toContain('missing');
		});
	});

	describe('post', () => {
		it('opens a POST request to /api/{endpoint}', async () => {
			const request = new ApiRequest('Login' as any);
			await request.post({ username: 'u', password: 'p' } as any);

			expect(lastXhr().open).toHaveBeenCalledWith('POST', '/api/Login', true);
		});

		it('sets content-type to application/json', async () => {
			const request = new ApiRequest('Login' as any);
			await request.post({ username: 'u', password: 'p' } as any);

			expect(lastXhr().setRequestHeader).toHaveBeenCalledWith('content-type', 'application/json');
		});

		it('sends JSON-stringified payload', async () => {
			const payload = { username: 'user', password: 'pass' };
			const request = new ApiRequest('Login' as any);
			await request.post(payload as any);

			expect(lastXhr().send).toHaveBeenCalledWith(JSON.stringify(payload));
		});

		it('resolves even when send throws', async () => {
			const request = new ApiRequest('Login' as any);
			const xhr = lastXhr();
			xhr.send = vi.fn(() => {
				throw new Error('Network error');
			});

			const response = await request.post({ username: 'u', password: 'p' } as any);
			expect(response).toBeDefined();
		});
	});

	describe('static get', () => {
		it('creates a request and returns data directly', async () => {
			const data = await ApiRequest.get('Items' as any);
			expect(data).toBe('test-result');
		});
	});

	describe('static post', () => {
		it('creates a request and returns data directly', async () => {
			const data = await ApiRequest.post('Login' as any, { username: 'u', password: 'p' } as any);
			expect(data).toBe('test-result');
		});
	});
});
