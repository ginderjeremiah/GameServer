import { describe, it, expect, vi, beforeEach, type Mock } from 'vitest';

vi.mock('svelte', () => ({
	onDestroy: vi.fn()
}));

// The close handler's auth-retry path delegates to auth.ts; mock it so the refresh/reconnect
// decision can be driven without touching the real token endpoints.
vi.mock('$lib/api/auth', () => ({
	refreshTokens: vi.fn(),
	handleAuthFailure: vi.fn()
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

import { ApiSocket, fetchSocketData, onSocketError } from '$lib/api/api-socket';
import { setTokens } from '$lib/api/token-store';
import { refreshTokens, handleAuthFailure } from '$lib/api/auth';

const flushMicrotasks = () => new Promise((resolve) => setTimeout(resolve, 0));

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
		vi.mocked(refreshTokens).mockReset();
		vi.mocked(handleAuthFailure).mockClear();
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

	describe('in-flight request lifecycle', () => {
		// The in-flight map is the internal bookkeeping the leak fix targets, so read it directly to
		// assert entries are added on send and pruned on settle.
		const inFlightSize = (s: ApiSocket) =>
			(s as unknown as { inFlightRequests: Map<string, unknown> }).inFlightRequests.size;

		it('prunes the in-flight entry once its response resolves', async () => {
			const promise = apiSocket.sendSocketCommand('DefeatEnemy', { timestamp: 1 });
			const ws = lastWs();
			const sent = JSON.parse(ws.send.mock.calls[0][0]);
			expect(inFlightSize(apiSocket)).toBe(1);

			receive(ws, JSON.stringify({ id: sent.id, name: 'DefeatEnemy', data: { cooldown: 10 } }));

			await promise;
			expect(inFlightSize(apiSocket)).toBe(0);
		});

		it('settles pending in-flight requests with an error when the socket closes', async () => {
			const promise = apiSocket.sendSocketCommand('DefeatEnemy', { timestamp: 1 });
			const ws = lastWs();
			expect(inFlightSize(apiSocket)).toBe(1);

			// No refresh token (localStorage cleared in beforeEach), so the close only settles — no reconnect.
			ws.readyState = ws.CLOSED;
			ws.onclose?.({ code: 1006 });

			const response = await promise;
			expect(response.error).toBeDefined();
			expect(inFlightSize(apiSocket)).toBe(0);
		});

		it('flushes queued-but-unsent commands after an auth-retry reconnect', async () => {
			setTokens({ accessToken: 'a', refreshToken: 'r' });
			vi.mocked(refreshTokens).mockResolvedValue({ accessToken: 'a2', refreshToken: 'r2' });

			// First socket is still CONNECTING, so the command sits queued (unsent) rather than in-flight.
			// A function expression (not an arrow) so the mock can still be invoked with `new WebSocket()`.
			webSocketMock.mockImplementationOnce(function (url?: string) {
				const ws = createMockWebSocket(url);
				ws.readyState = 0;
				return ws;
			});

			apiSocket.sendSocketCommand('DefeatEnemy', { timestamp: 1 });
			const connecting = lastWs();
			expect(connecting.send).not.toHaveBeenCalled();
			expect(inFlightSize(apiSocket)).toBe(0);

			// Handshake auth-rejected: closes before opening, triggering the refresh-and-reconnect path.
			connecting.readyState = connecting.CLOSED;
			connecting.onclose?.({ code: 1006 });
			await flushMicrotasks();

			const reconnected = lastWs();
			expect(reconnected).not.toBe(connecting);
			const sent = JSON.parse(reconnected.send.mock.calls[0][0]);
			expect(sent.name).toBe('DefeatEnemy');
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

	describe('socket error propagation', () => {
		it('notifies onSocketError listeners when the socket errors', () => {
			const consoleSpy = vi.spyOn(console, 'error').mockImplementation(() => {});
			const handler = vi.fn();
			onSocketError(handler, false);

			apiSocket.sendSocketCommand('DefeatEnemy', { timestamp: 1 });
			const ws = lastWs();
			ws.onerror?.();

			// Hook listeners also receive an unhook fn as a trailing arg, so assert on the message only.
			expect(handler).toHaveBeenCalled();
			expect(handler.mock.calls[0][0]).toBe('WebSocket connection error');
			consoleSpy.mockRestore();
		});
	});

	describe('handleClose auth retry', () => {
		// Drive a socket to the "rejected handshake" state: created but never opened, then closed
		// with a non-normal code while a refresh token is available.
		const closeUnopened = (ws: MockWebSocket, code = 1006) => {
			ws.readyState = ws.CLOSED;
			ws.onclose?.({ code });
		};

		it('refreshes once and reconnects when the handshake is auth-rejected', async () => {
			setTokens({ accessToken: 'a', refreshToken: 'r' });
			vi.mocked(refreshTokens).mockResolvedValue({ accessToken: 'a2', refreshToken: 'r2' });

			apiSocket.sendSocketCommand('DefeatEnemy', { timestamp: 1 });
			const ws = lastWs();
			webSocketMock.mockClear();

			closeUnopened(ws);
			await flushMicrotasks();

			expect(refreshTokens).toHaveBeenCalledTimes(1);
			// A successful refresh re-opens the connection (a fresh socket is created).
			expect(webSocketMock).toHaveBeenCalledTimes(1);
			expect(handleAuthFailure).not.toHaveBeenCalled();
		});

		it('routes to the auth-failure handler when the refresh fails', async () => {
			setTokens({ accessToken: 'a', refreshToken: 'r' });
			vi.mocked(refreshTokens).mockResolvedValue(null);

			apiSocket.sendSocketCommand('DefeatEnemy', { timestamp: 1 });
			const ws = lastWs();

			closeUnopened(ws);
			await flushMicrotasks();

			expect(refreshTokens).toHaveBeenCalledTimes(1);
			expect(handleAuthFailure).toHaveBeenCalledTimes(1);
		});

		it('does not retry a socket that had already opened', async () => {
			setTokens({ accessToken: 'a', refreshToken: 'r' });

			apiSocket.sendSocketCommand('DefeatEnemy', { timestamp: 1 });
			const ws = lastWs();
			ws.onopen?.(); // marks the socket as opened

			closeUnopened(ws);
			await flushMicrotasks();

			expect(refreshTokens).not.toHaveBeenCalled();
		});

		it('does not retry on a normal closure', async () => {
			setTokens({ accessToken: 'a', refreshToken: 'r' });

			apiSocket.sendSocketCommand('DefeatEnemy', { timestamp: 1 });
			const ws = lastWs();

			closeUnopened(ws, 1000);
			await flushMicrotasks();

			expect(refreshTokens).not.toHaveBeenCalled();
		});

		it('does not retry when there is no refresh token to use', async () => {
			// localStorage cleared in beforeEach → getRefreshToken() returns null.
			apiSocket.sendSocketCommand('DefeatEnemy', { timestamp: 1 });
			const ws = lastWs();

			closeUnopened(ws);
			await flushMicrotasks();

			expect(refreshTokens).not.toHaveBeenCalled();
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
