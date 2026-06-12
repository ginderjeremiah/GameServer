import { describe, it, expect } from 'vitest';
import { ApiResponse, type RawApiResponse } from '$lib/api/api-response';

const makeRaw = (overrides: Partial<RawApiResponse> = {}): RawApiResponse => {
	return {
		status: 200,
		statusText: 'OK',
		responseText: JSON.stringify({ data: null, errorMessage: undefined }),
		...overrides
	};
};

describe('ApiResponse', () => {
	describe('data', () => {
		it('returns parsed data from response', () => {
			const raw = makeRaw({
				responseText: JSON.stringify({ data: { id: 1, name: 'Test' } })
			});
			const response = new ApiResponse(raw);

			expect(response.data).toEqual({ id: 1, name: 'Test' });
		});

		it('returns array data', () => {
			const raw = makeRaw({
				responseText: JSON.stringify({ data: [1, 2, 3] })
			});
			const response = new ApiResponse(raw);

			expect(response.data).toEqual([1, 2, 3]);
		});

		it('throws when data is null and errorMessage exists', () => {
			const raw = makeRaw({
				responseText: JSON.stringify({ data: null, errorMessage: 'Not found' })
			});
			const response = new ApiResponse(raw);

			expect(() => response.data).toThrow('Not found');
		});

		it('returns null data when no errorMessage', () => {
			const raw = makeRaw({
				responseText: JSON.stringify({ data: null })
			});
			const response = new ApiResponse(raw);

			expect(response.data).toBeNull();
		});
	});

	describe('error', () => {
		it('returns errorMessage from response body', () => {
			const raw = makeRaw({
				responseText: JSON.stringify({ data: null, errorMessage: 'Bad request' })
			});
			const response = new ApiResponse(raw);

			expect(response.error).toBe('Bad request');
		});

		it('falls back to statusText when no errorMessage', () => {
			const raw = makeRaw({
				statusText: 'Internal Server Error',
				responseText: JSON.stringify({ data: null })
			});
			const response = new ApiResponse(raw);

			expect(response.error).toBe('Internal Server Error');
		});

		it('falls back to default message when no errorMessage or statusText', () => {
			const raw = makeRaw({
				statusText: '',
				responseText: JSON.stringify({ data: null })
			});
			const response = new ApiResponse(raw);

			expect(response.error).toBe('Failed to communicate with server.');
		});
	});

	describe('ok', () => {
		it('is true for a 2xx response with no error message', () => {
			const response = new ApiResponse(makeRaw({ responseText: JSON.stringify({ data: null }) }));
			expect(response.ok).toBe(true);
		});

		it('is true for a bodyless 2xx success (ApiResponse.Success())', () => {
			const response = new ApiResponse(makeRaw({ responseText: '' }));
			expect(response.ok).toBe(true);
		});

		it('is false for a 2xx response that carries an error message (a business failure)', () => {
			const raw = makeRaw({
				responseText: JSON.stringify({ data: null, errorMessage: 'Failed to equip item.' })
			});
			expect(new ApiResponse(raw).ok).toBe(false);
		});

		it('is false for a non-2xx status', () => {
			const response = new ApiResponse(makeRaw({ status: 500, statusText: 'Internal Server Error' }));
			expect(response.ok).toBe(false);
		});

		it('is false for a network error (status 0, empty body)', () => {
			const response = new ApiResponse(makeRaw({ status: 0, statusText: '', responseText: '' }));
			expect(response.ok).toBe(false);
		});
	});

	describe('status', () => {
		it('returns HTTP status code', () => {
			const response = new ApiResponse(makeRaw({ status: 404 }));
			expect(response.status).toBe(404);
		});
	});

	describe('responseText', () => {
		it('returns raw response text', () => {
			const text = '{"data": "raw"}';
			const response = new ApiResponse(makeRaw({ responseText: text }));
			expect(response.responseText).toBe(text);
		});
	});

	describe('empty response', () => {
		it('handles empty responseText gracefully', () => {
			const response = new ApiResponse(makeRaw({ responseText: '' }));
			expect(response.data).toBeUndefined();
		});
	});

	describe('malformed response', () => {
		it('returns structured error for unparseable JSON', () => {
			const response = new ApiResponse(makeRaw({ responseText: 'not-json' }));
			expect(() => response.data).toThrow('Invalid server response.');
		});
	});

	describe('json caching', () => {
		it('parses JSON only once (lazy caching)', () => {
			const raw = makeRaw({
				responseText: JSON.stringify({ data: 'cached' })
			});
			const response = new ApiResponse(raw);

			const first = response.data;
			const second = response.data;
			expect(first).toBe(second);
		});
	});
});
