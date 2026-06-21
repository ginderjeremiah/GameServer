import { describe, it, expect, vi } from 'vitest';
import { ApiSocketRequest } from '$lib/api/api-socket-request';
import type { IApiSocketResponse } from '$lib/api/api-socket';

// ApiSocketRequest is the pure transport primitive the socket client builds on: it wraps a single
// command's pending promise and exposes the resolve-with-error contract the docs lean on (every
// failure resolves the promise with an `error` field, never rejects). It has no DOM/WebSocket
// dependencies, so it is exercised directly here rather than only indirectly through ApiSocket mocks.
describe('ApiSocketRequest', () => {
	describe('getCommandInfo', () => {
		it('serializes the parameters to a JSON string alongside the id and command name', () => {
			const request = new ApiSocketRequest('7', 'DefeatEnemy', { clientTotalMs: 42 });

			expect(request.getCommandInfo()).toEqual({
				id: '7',
				name: 'DefeatEnemy',
				parameters: JSON.stringify({ clientTotalMs: 42 })
			});
		});

		it('serializes a no-request command with undefined parameters', () => {
			// A no-request command (GetZones) carries no parameters; JSON.stringify(undefined) yields undefined.
			const request = new ApiSocketRequest('0', 'GetZones');

			const info = request.getCommandInfo();
			expect(info.id).toBe('0');
			expect(info.name).toBe('GetZones');
			expect(info.parameters).toBeUndefined();
		});
	});

	describe('resolve', () => {
		it('settles getResponse with the full success response', async () => {
			const request = new ApiSocketRequest('1', 'DefeatEnemy', { clientTotalMs: 1 });
			const response: IApiSocketResponse<'DefeatEnemy'> = { id: '1', name: 'DefeatEnemy', data: { cooldown: 100 } };

			request.resolve(response);

			await expect(request.getResponse()).resolves.toBe(response);
		});
	});

	describe('settleWithError', () => {
		it('settles getResponse through the resolve-with-error contract (error field, no data)', async () => {
			const request = new ApiSocketRequest('2', 'DefeatEnemy', { clientTotalMs: 1 });

			request.settleWithError('Connection lost. Please try again.');

			const response = await request.getResponse();
			expect(response.id).toBe('2');
			expect(response.name).toBe('DefeatEnemy');
			expect(response.error).toBe('Connection lost. Please try again.');
			// The error response carries no data, matching how the server signals errors so callers fall
			// through their existing `response.error` guard rather than reading a partial payload.
			expect(response.data).toBeUndefined();
		});

		it('resolves rather than rejects, so callers never need a try/catch around getResponse', async () => {
			const request = new ApiSocketRequest('3', 'DefeatEnemy', { clientTotalMs: 1 });
			const onRejected = vi.fn();

			request.settleWithError('boom');
			await request.getResponse().catch(onRejected);

			expect(onRejected).not.toHaveBeenCalled();
		});
	});

	// Backed by Promise.withResolvers, so only the first settle takes effect. This is the request-level
	// backstop the socket client relies on when a request can be settled by two paths — e.g. a per-request
	// timeout firing and then a socket close also iterating the in-flight map (see ApiSocket).
	describe('single settlement (first settle wins)', () => {
		it('keeps the first success when a later error settle arrives', async () => {
			const request = new ApiSocketRequest('4', 'DefeatEnemy', { clientTotalMs: 1 });
			const success: IApiSocketResponse<'DefeatEnemy'> = { id: '4', name: 'DefeatEnemy', data: { cooldown: 5 } };

			request.resolve(success);
			request.settleWithError('too late');

			await expect(request.getResponse()).resolves.toBe(success);
		});

		it('keeps the first error when a later success settle arrives', async () => {
			const request = new ApiSocketRequest('5', 'DefeatEnemy', { clientTotalMs: 1 });

			request.settleWithError('first');
			request.resolve({ id: '5', name: 'DefeatEnemy', data: { cooldown: 9 } });

			const response = await request.getResponse();
			expect(response.error).toBe('first');
			expect(response.data).toBeUndefined();
		});
	});
});
