import { describe, it, expect } from 'vitest';
import { ApiResponse } from '$lib/api/api-response';

const makeXhr = (overrides: Partial<XMLHttpRequest> = {}): XMLHttpRequest => {
	return {
		status: 200,
		statusText: 'OK',
		responseText: JSON.stringify({ data: null, errorMessage: undefined }),
		...overrides
	} as XMLHttpRequest;
};

describe('ApiResponse', () => {
	describe('data', () => {
		it('returns parsed data from response', () => {
			const xhr = makeXhr({
				responseText: JSON.stringify({ data: { id: 1, name: 'Test' } })
			});
			const response = new ApiResponse(xhr);

			expect(response.data).toEqual({ id: 1, name: 'Test' });
		});

		it('returns array data', () => {
			const xhr = makeXhr({
				responseText: JSON.stringify({ data: [1, 2, 3] })
			});
			const response = new ApiResponse(xhr);

			expect(response.data).toEqual([1, 2, 3]);
		});

		it('throws when data is null and errorMessage exists', () => {
			const xhr = makeXhr({
				responseText: JSON.stringify({ data: null, errorMessage: 'Not found' })
			});
			const response = new ApiResponse(xhr);

			expect(() => response.data).toThrow('Not found');
		});

		it('returns null data when no errorMessage', () => {
			const xhr = makeXhr({
				responseText: JSON.stringify({ data: null })
			});
			const response = new ApiResponse(xhr);

			expect(response.data).toBeNull();
		});
	});

	describe('error', () => {
		it('returns errorMessage from response body', () => {
			const xhr = makeXhr({
				responseText: JSON.stringify({ data: null, errorMessage: 'Bad request' })
			});
			const response = new ApiResponse(xhr);

			expect(response.error).toBe('Bad request');
		});

		it('falls back to statusText when no errorMessage', () => {
			const xhr = makeXhr({
				statusText: 'Internal Server Error',
				responseText: JSON.stringify({ data: null })
			});
			const response = new ApiResponse(xhr);

			expect(response.error).toBe('Internal Server Error');
		});

		it('falls back to default message when no errorMessage or statusText', () => {
			const xhr = makeXhr({
				statusText: '',
				responseText: JSON.stringify({ data: null })
			});
			const response = new ApiResponse(xhr);

			expect(response.error).toBe('Failed to communicate with server.');
		});
	});

	describe('status', () => {
		it('returns HTTP status code', () => {
			const response = new ApiResponse(makeXhr({ status: 404 }));
			expect(response.status).toBe(404);
		});
	});

	describe('responseText', () => {
		it('returns raw response text', () => {
			const text = '{"data": "raw"}';
			const response = new ApiResponse(makeXhr({ responseText: text }));
			expect(response.responseText).toBe(text);
		});
	});

	describe('empty response', () => {
		it('handles empty responseText gracefully', () => {
			const response = new ApiResponse(makeXhr({ responseText: '' }));
			expect(response.data).toBeUndefined();
		});
	});

	describe('malformed response', () => {
		it('returns structured error for unparseable JSON', () => {
			const response = new ApiResponse(makeXhr({ responseText: 'not-json' }));
			expect(() => response.data).toThrow('Invalid server response.');
		});
	});

	describe('json caching', () => {
		it('parses JSON only once (lazy caching)', () => {
			const xhr = makeXhr({
				responseText: JSON.stringify({ data: 'cached' })
			});
			const response = new ApiResponse(xhr);

			const first = response.data;
			const second = response.data;
			expect(first).toBe(second);
		});
	});
});
