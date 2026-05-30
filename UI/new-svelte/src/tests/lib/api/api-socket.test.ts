import { describe, it, expect, vi, beforeEach } from 'vitest';

vi.mock('svelte', () => ({
	onDestroy: vi.fn()
}));

let socketInstances: any[];

vi.stubGlobal('WebSocket', function (this: any) {
	this.readyState = 1;
	this.OPEN = 1;
	this.CLOSED = 3;
	this.send = vi.fn();
	this.onopen = null;
	this.onmessage = null;
	this.onerror = null;
	socketInstances.push(this);
});

import { ApiSocket } from '$lib/api/api-socket';

describe('ApiSocket', () => {
	let apiSocket: ApiSocket;

	beforeEach(() => {
		// Force the module-level socket to be seen as CLOSED so ensureSocket creates a new one
		for (const s of socketInstances ?? []) {
			s.readyState = 3; // CLOSED
		}
		socketInstances = [];
		apiSocket = new ApiSocket();
	});

	function lastWs() {
		return socketInstances[socketInstances.length - 1];
	}

	describe('sendSocketCommand', () => {
		it('creates a WebSocket and sends the command', async () => {
			const promise = apiSocket.sendSocketCommand('DefeatEnemy' as any);
			const ws = lastWs();

			expect(ws.send).toHaveBeenCalledTimes(1);
			const sent = JSON.parse(ws.send.mock.calls[0][0]);
			expect(sent.name).toBe('DefeatEnemy');
			expect(sent.id).toBeDefined();

			ws.onmessage({
				data: JSON.stringify({ id: sent.id, name: 'DefeatEnemy', data: { exp: 100 } })
			});

			const response = await promise;
			expect(response.data).toEqual({ exp: 100 });
		});

		it('assigns incrementing command IDs', async () => {
			const p1 = apiSocket.sendSocketCommand('DefeatEnemy' as any);
			const p2 = apiSocket.sendSocketCommand('NewEnemy' as any);
			const ws = lastWs();

			const sent1 = JSON.parse(ws.send.mock.calls[0][0]);
			const sent2 = JSON.parse(ws.send.mock.calls[1][0]);
			expect(Number(sent2.id)).toBeGreaterThan(Number(sent1.id));

			ws.onmessage({ data: JSON.stringify({ id: sent1.id, name: 'DefeatEnemy', data: {} }) });
			ws.onmessage({ data: JSON.stringify({ id: sent2.id, name: 'NewEnemy', data: {} }) });

			await p1;
			await p2;
		});
	});

	describe('listenCommand', () => {
		it('calls listener when matching command arrives', () => {
			const listener = vi.fn();
			apiSocket.listenCommand('SocketReplaced' as any, listener, false);

			apiSocket.sendSocketCommand('DefeatEnemy' as any);
			const ws = lastWs();

			ws.onmessage({
				data: JSON.stringify({ name: 'SocketReplaced', data: {} })
			});

			expect(listener).toHaveBeenCalledTimes(1);
		});

		it('supports multiple listeners for the same command', () => {
			const listener1 = vi.fn();
			const listener2 = vi.fn();
			apiSocket.listenCommand('SocketReplaced' as any, listener1, false);
			apiSocket.listenCommand('SocketReplaced' as any, listener2, false);

			apiSocket.sendSocketCommand('DefeatEnemy' as any);
			const ws = lastWs();

			ws.onmessage({
				data: JSON.stringify({ name: 'SocketReplaced', data: {} })
			});

			expect(listener1).toHaveBeenCalledTimes(1);
			expect(listener2).toHaveBeenCalledTimes(1);
		});

		it('does not call listeners for different commands', () => {
			const listener = vi.fn();
			apiSocket.listenCommand('SocketReplaced' as any, listener, false);

			apiSocket.sendSocketCommand('DefeatEnemy' as any);
			const ws = lastWs();

			ws.onmessage({
				data: JSON.stringify({ id: '0', name: 'DefeatEnemy', data: {} })
			});

			expect(listener).not.toHaveBeenCalled();
		});
	});

	describe('ping/pong', () => {
		it('responds with pong when ping received', () => {
			apiSocket.sendSocketCommand('DefeatEnemy' as any);
			const ws = lastWs();

			ws.onmessage({ data: 'ping' });

			expect(ws.send).toHaveBeenCalledWith('pong');
		});
	});

	describe('error handling', () => {
		it('logs error and does not crash on malformed JSON', () => {
			const consoleSpy = vi.spyOn(console, 'error').mockImplementation(() => {});

			apiSocket.sendSocketCommand('DefeatEnemy' as any);
			const ws = lastWs();
			ws.onmessage({ data: 'not-json' });

			expect(consoleSpy).toHaveBeenCalled();
			consoleSpy.mockRestore();
		});

		it('catches listener callback errors and logs them', () => {
			const consoleSpy = vi.spyOn(console, 'error').mockImplementation(() => {});
			const badListener = vi.fn(() => {
				throw new Error('listener error');
			});

			apiSocket.listenCommand('SocketReplaced' as any, badListener, false);

			apiSocket.sendSocketCommand('DefeatEnemy' as any);
			const ws = lastWs();
			ws.onmessage({
				data: JSON.stringify({ name: 'SocketReplaced', data: {} })
			});

			expect(badListener).toHaveBeenCalled();
			expect(consoleSpy).toHaveBeenCalled();
			consoleSpy.mockRestore();
		});
	});

	describe('listenCommand unhook', () => {
		it('returns an unhook function that removes the listener', () => {
			const listener = vi.fn();
			const unhook = apiSocket.listenCommand('SocketReplaced' as any, listener, false);

			apiSocket.sendSocketCommand('DefeatEnemy' as any);
			const ws = lastWs();

			ws.onmessage({
				data: JSON.stringify({ name: 'SocketReplaced', data: {} })
			});
			expect(listener).toHaveBeenCalledTimes(1);

			unhook();

			ws.onmessage({
				data: JSON.stringify({ name: 'SocketReplaced', data: {} })
			});
			expect(listener).toHaveBeenCalledTimes(1);
		});
	});
});
