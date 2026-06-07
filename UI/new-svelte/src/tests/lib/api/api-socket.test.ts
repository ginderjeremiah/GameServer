import { describe, it, expect, vi, beforeEach, type Mock } from 'vitest';

vi.mock('svelte', () => ({
	onDestroy: vi.fn()
}));

interface MockWebSocket {
	readyState: number;
	OPEN: number;
	CLOSED: number;
	send: Mock;
	onopen: (() => void) | null;
	onmessage: ((ev: { data: string }) => void) | null;
	onerror: (() => void) | null;
	onclose: ((ev: { code: number }) => void) | null;
}

let socketInstances: MockWebSocket[] = [];
let lastSocketUrl: string | undefined;

function createMockWebSocket(url?: string): MockWebSocket {
	lastSocketUrl = url;
	const ws: MockWebSocket = {
		readyState: 1,
		OPEN: 1,
		CLOSED: 3,
		send: vi.fn(),
		onopen: null,
		onmessage: null,
		onerror: null,
		onclose: null
	};
	socketInstances.push(ws);
	return ws;
}

const webSocketMock = vi.fn(createMockWebSocket);
vi.stubGlobal('WebSocket', webSocketMock);

import { ApiSocket, fetchSocketData } from '$lib/api/api-socket';
import { setTokens } from '$lib/api/token-store';

describe('ApiSocket', () => {
	let apiSocket: ApiSocket;

	beforeEach(() => {
		// Force any socket left from a previous test to read as CLOSED so ensureSocket creates a fresh one.
		for (const s of socketInstances) {
			s.readyState = s.CLOSED;
		}
		socketInstances = [];
		lastSocketUrl = undefined;
		webSocketMock.mockClear();
		localStorage.clear();
		apiSocket = new ApiSocket();
	});

	const lastWs = () => socketInstances[socketInstances.length - 1];

	const receive = (ws: MockWebSocket, data: string) => {
		if (!ws.onmessage) {
			throw new Error('WebSocket has no message handler registered');
		}
		ws.onmessage({ data });
	};

	describe('authentication', () => {
		it('passes the stored access token as the access_token query parameter', () => {
			setTokens({ accessToken: 'token-123', refreshToken: 'r' });

			apiSocket.sendSocketCommand('DefeatEnemy', { timestamp: 1 });

			expect(lastSocketUrl).toBe('/socket?access_token=token-123');
		});

		it('opens an unauthenticated socket when no token is stored', () => {
			apiSocket.sendSocketCommand('DefeatEnemy', { timestamp: 1 });

			expect(lastSocketUrl).toBe('/socket');
		});
	});

	describe('sendSocketCommand', () => {
		it('creates a WebSocket and sends the command', async () => {
			const promise = apiSocket.sendSocketCommand('DefeatEnemy', { timestamp: 1 });
			const ws = lastWs();

			expect(ws.send).toHaveBeenCalledTimes(1);
			const sent = JSON.parse(ws.send.mock.calls[0][0]);
			expect(sent.name).toBe('DefeatEnemy');
			expect(sent.id).toBeDefined();

			receive(ws, JSON.stringify({ id: sent.id, name: 'DefeatEnemy', data: { cooldown: 100 } }));

			const response = await promise;
			expect(response.data).toEqual({ cooldown: 100 });
		});

		it('assigns incrementing command IDs', async () => {
			const p1 = apiSocket.sendSocketCommand('DefeatEnemy', { timestamp: 1 });
			const p2 = apiSocket.sendSocketCommand('NewEnemy', { newZoneId: 1 });
			const ws = lastWs();

			const sent1 = JSON.parse(ws.send.mock.calls[0][0]);
			const sent2 = JSON.parse(ws.send.mock.calls[1][0]);
			expect(Number(sent2.id)).toBeGreaterThan(Number(sent1.id));

			receive(ws, JSON.stringify({ id: sent1.id, name: 'DefeatEnemy', data: {} }));
			receive(ws, JSON.stringify({ id: sent2.id, name: 'NewEnemy', data: {} }));

			await p1;
			await p2;
		});
	});

	describe('listenCommand', () => {
		it('calls listener when matching command arrives', () => {
			const listener = vi.fn();
			apiSocket.listenCommand('SocketReplaced', listener, false);

			apiSocket.sendSocketCommand('DefeatEnemy', { timestamp: 1 });
			const ws = lastWs();

			receive(ws, JSON.stringify({ name: 'SocketReplaced', data: {} }));

			expect(listener).toHaveBeenCalledTimes(1);
		});

		it('supports multiple listeners for the same command', () => {
			const listener1 = vi.fn();
			const listener2 = vi.fn();
			apiSocket.listenCommand('SocketReplaced', listener1, false);
			apiSocket.listenCommand('SocketReplaced', listener2, false);

			apiSocket.sendSocketCommand('DefeatEnemy', { timestamp: 1 });
			const ws = lastWs();

			receive(ws, JSON.stringify({ name: 'SocketReplaced', data: {} }));

			expect(listener1).toHaveBeenCalledTimes(1);
			expect(listener2).toHaveBeenCalledTimes(1);
		});

		it('does not call listeners for different commands', () => {
			const listener = vi.fn();
			apiSocket.listenCommand('SocketReplaced', listener, false);

			apiSocket.sendSocketCommand('DefeatEnemy', { timestamp: 1 });
			const ws = lastWs();

			receive(ws, JSON.stringify({ id: '0', name: 'DefeatEnemy', data: {} }));

			expect(listener).not.toHaveBeenCalled();
		});
	});

	describe('ping/pong', () => {
		it('responds with pong when ping received', () => {
			apiSocket.sendSocketCommand('DefeatEnemy', { timestamp: 1 });
			const ws = lastWs();

			receive(ws, 'ping');

			expect(ws.send).toHaveBeenCalledWith('pong');
		});
	});

	describe('error handling', () => {
		it('logs error and does not crash on malformed JSON', () => {
			const consoleSpy = vi.spyOn(console, 'error').mockImplementation(() => {});

			apiSocket.sendSocketCommand('DefeatEnemy', { timestamp: 1 });
			const ws = lastWs();
			receive(ws, 'not-json');

			expect(consoleSpy).toHaveBeenCalled();
			consoleSpy.mockRestore();
		});

		it('catches listener callback errors and logs them', () => {
			const consoleSpy = vi.spyOn(console, 'error').mockImplementation(() => {});
			const badListener = vi.fn(() => {
				throw new Error('listener error');
			});

			apiSocket.listenCommand('SocketReplaced', badListener, false);

			apiSocket.sendSocketCommand('DefeatEnemy', { timestamp: 1 });
			const ws = lastWs();
			receive(ws, JSON.stringify({ name: 'SocketReplaced', data: {} }));

			expect(badListener).toHaveBeenCalled();
			expect(consoleSpy).toHaveBeenCalled();
			consoleSpy.mockRestore();
		});
	});

	// fetchSocketData wraps the module-level `apiSocket` singleton (not the per-test instance above),
	// driving it through the same mocked WebSocket. It mirrors ApiRequest.get's throw-on-error contract.
	describe('fetchSocketData', () => {
		it('resolves with the response data when the command succeeds', async () => {
			const promise = fetchSocketData('GetZones');
			const ws = lastWs();
			const sent = JSON.parse(ws.send.mock.calls[0][0]);
			expect(sent.name).toBe('GetZones');

			receive(ws, JSON.stringify({ id: sent.id, name: 'GetZones', data: [{ id: 0, name: 'Zone' }] }));

			await expect(promise).resolves.toEqual([{ id: 0, name: 'Zone' }]);
		});

		it('throws when the server reports an error', async () => {
			const promise = fetchSocketData('GetZones');
			const ws = lastWs();
			const sent = JSON.parse(ws.send.mock.calls[0][0]);

			receive(ws, JSON.stringify({ id: sent.id, name: 'GetZones', error: 'Server boom' }));

			await expect(promise).rejects.toThrow('Server boom');
		});
	});

	describe('listenCommand unhook', () => {
		it('returns an unhook function that removes the listener', () => {
			const listener = vi.fn();
			const unhook = apiSocket.listenCommand('SocketReplaced', listener, false);

			apiSocket.sendSocketCommand('DefeatEnemy', { timestamp: 1 });
			const ws = lastWs();

			receive(ws, JSON.stringify({ name: 'SocketReplaced', data: {} }));
			expect(listener).toHaveBeenCalledTimes(1);

			unhook();

			receive(ws, JSON.stringify({ name: 'SocketReplaced', data: {} }));
			expect(listener).toHaveBeenCalledTimes(1);
		});
	});
});
