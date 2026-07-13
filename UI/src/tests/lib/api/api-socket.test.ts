import { describe, it, expect, vi, beforeEach, afterEach, type Mock } from 'vitest';

vi.mock('svelte', () => ({
	onDestroy: vi.fn()
}));

// The open path pre-emptively refreshes via ensureValidAccessToken and the close handler's auth-retry
// path delegates to refreshTokens; mock auth.ts so both can be driven without touching the real token
// endpoints. ensureValidAccessToken defaults (in beforeEach) to returning the stored access token, so the
// handshake-URL tests still exercise "whatever token the open path resolves ends up in the URL".
vi.mock('$lib/api/auth', () => ({
	ensureValidAccessToken: vi.fn(),
	refreshTokens: vi.fn(),
	handleAuthFailure: vi.fn()
}));

interface MockWebSocket {
	readyState: number;
	OPEN: number;
	CLOSED: number;
	send: Mock;
	close: Mock;
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
		close: vi.fn(),
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
import { setTokens, getAccessToken, clearTokens } from '$lib/api/token-store';
import { ensureValidAccessToken, refreshTokens, handleAuthFailure } from '$lib/api/auth';

// Opening a socket is now asynchronous (it awaits the pre-emptive refresh before `new WebSocket`), so a
// command no longer creates the socket synchronously. Drain the promise microtask queue to let the
// open → queue-flush chain settle. Microtask-based (not setTimeout) so it also works under fake timers.
const flushMicrotasks = async () => {
	for (let i = 0; i < 10; i++) {
		await Promise.resolve();
	}
};

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
		// Default: the open path resolves to the currently stored access token (no implicit refresh), so
		// URL assertions reflect the token in the store rather than triggering the real refresh machinery.
		vi.mocked(ensureValidAccessToken).mockReset();
		vi.mocked(ensureValidAccessToken).mockImplementation(() => Promise.resolve(getAccessToken()));
		apiSocket = new ApiSocket();
	});

	afterEach(() => {
		// Tear down the keepalive interval the instance may have armed so it doesn't leak across tests.
		apiSocket.disconnect();
	});

	const lastWs = () => socketInstances[socketInstances.length - 1];

	// The lifecycle fixes touch internal timer/counter bookkeeping, so read it directly to assert the
	// keepalive interval is cleared and the auth-retry budget is bounded.
	const internals = (s: ApiSocket) =>
		s as unknown as { pingIntervalId: ReturnType<typeof setInterval> | null; socketAuthRetries: number };

	const receive = (ws: MockWebSocket, data: string) => {
		if (!ws.onmessage) {
			throw new Error('WebSocket has no message handler registered');
		}
		ws.onmessage({ data });
	};

	describe('authentication', () => {
		it('passes the stored access token as the access_token query parameter', async () => {
			setTokens({ accessToken: 'token-123', refreshToken: 'r' });

			apiSocket.sendSocketCommand('DefeatEnemy', { clientTotalMs: 1 });
			await flushMicrotasks();

			expect(lastSocketUrl).toBe('/socket?access_token=token-123');
		});

		it('opens an unauthenticated socket when no token is stored', async () => {
			apiSocket.sendSocketCommand('DefeatEnemy', { clientTotalMs: 1 });
			await flushMicrotasks();

			expect(lastSocketUrl).toBe('/socket');
			// A never-logged-in caller (no prior refresh token) must not be routed to the auth-failure path.
			expect(handleAuthFailure).not.toHaveBeenCalled();
		});
	});

	describe('pre-emptive token refresh on open', () => {
		it('refreshes the access token pre-emptively and opens the handshake with the refreshed token', async () => {
			setTokens({ accessToken: 'stale', refreshToken: 'r' });
			// The open path mints a fresh token before building the handshake URL.
			vi.mocked(ensureValidAccessToken).mockResolvedValue('fresh-token');

			apiSocket.sendSocketCommand('DefeatEnemy', { clientTotalMs: 1 });
			await flushMicrotasks();

			expect(ensureValidAccessToken).toHaveBeenCalled();
			expect(lastSocketUrl).toBe('/socket?access_token=fresh-token');
		});

		it('opens only one socket when concurrent callers race the async open', async () => {
			setTokens({ accessToken: 'a', refreshToken: 'r' });
			// Hold the pre-emptive refresh open so both callers pass the "is there a socket?" check while
			// awaiting, exercising the single-flight guard.
			let releaseRefresh: (token: string | null) => void = () => {};
			vi.mocked(ensureValidAccessToken).mockReturnValue(
				new Promise<string | null>((resolve) => {
					releaseRefresh = resolve;
				})
			);

			apiSocket.sendSocketCommand('DefeatEnemy', { clientTotalMs: 1 });
			apiSocket.attemptPing(); // a second concurrent open attempt while the refresh is still in flight

			// Both callers are awaiting the single in-flight refresh; no socket exists yet.
			expect(webSocketMock).not.toHaveBeenCalled();
			expect(ensureValidAccessToken).toHaveBeenCalledTimes(1);

			releaseRefresh('a');
			await flushMicrotasks();

			expect(webSocketMock).toHaveBeenCalledTimes(1);
		});

		it('routes to auth failure (without opening) when the pre-emptive refresh fails for a logged-in session', async () => {
			setTokens({ accessToken: 'expired', refreshToken: 'r' });
			// Refresh token spent/revoked: no usable token can be obtained. The real ensureValidAccessToken
			// clears storage itself in this case (via refreshTokens), so the mock mirrors that.
			vi.mocked(ensureValidAccessToken).mockImplementation(async () => {
				clearTokens();
				return null;
			});

			const promise = apiSocket.sendSocketCommand('DefeatEnemy', { clientTotalMs: 1 });
			await flushMicrotasks();

			expect(handleAuthFailure).toHaveBeenCalledTimes(1);
			// No doomed unauthenticated handshake is opened, and the keepalive is not left running.
			expect(webSocketMock).not.toHaveBeenCalled();
			expect(internals(apiSocket).pingIntervalId).toBeNull();
			// The queued command was never sent (no socket ever opened) and nothing will reconnect to
			// re-flush it, so it must settle with an error rather than await a response forever.
			await expect(promise).resolves.toMatchObject({ error: expect.any(String) });
		});

		it("recovers using a concurrent tab's rotated pair instead of tearing down the session", async () => {
			setTokens({ accessToken: 'expired', refreshToken: 'r' });
			// Simulate another tab winning the refresh race and rotating in a fresh pair while this tab's
			// own pre-emptive refresh was still resolving to null.
			vi.mocked(ensureValidAccessToken).mockImplementation(async () => {
				setTokens({ accessToken: 'winner-access', refreshToken: 'winner-refresh' });
				return null;
			});

			apiSocket.sendSocketCommand('DefeatEnemy', { clientTotalMs: 1 });
			await flushMicrotasks();

			expect(handleAuthFailure).not.toHaveBeenCalled();
			expect(lastSocketUrl).toBe('/socket?access_token=winner-access');
		});
	});

	describe('sendSocketCommand', () => {
		it('creates a WebSocket and sends the command', async () => {
			const promise = apiSocket.sendSocketCommand('DefeatEnemy', { clientTotalMs: 1 });
			await flushMicrotasks();
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
			const p1 = apiSocket.sendSocketCommand('DefeatEnemy', { clientTotalMs: 1 });
			const p2 = apiSocket.sendSocketCommand('NewEnemy', { newZoneId: 1, forceAbandon: false });
			await flushMicrotasks();
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
			const promise = apiSocket.sendSocketCommand('DefeatEnemy', { clientTotalMs: 1 });
			await flushMicrotasks();
			const ws = lastWs();
			const sent = JSON.parse(ws.send.mock.calls[0][0]);
			expect(inFlightSize(apiSocket)).toBe(1);

			receive(ws, JSON.stringify({ id: sent.id, name: 'DefeatEnemy', data: { cooldown: 10 } }));

			await promise;
			expect(inFlightSize(apiSocket)).toBe(0);
		});

		it('settles pending in-flight requests with an error when the socket closes', async () => {
			const promise = apiSocket.sendSocketCommand('DefeatEnemy', { clientTotalMs: 1 });
			await flushMicrotasks();
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

			apiSocket.sendSocketCommand('DefeatEnemy', { clientTotalMs: 1 });
			await flushMicrotasks();
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

	describe('per-request timeout', () => {
		const inFlightSize = (s: ApiSocket) =>
			(s as unknown as { inFlightRequests: Map<string, unknown> }).inFlightRequests.size;

		it('settles a sent request with an error once the timeout elapses', async () => {
			const warnSpy = vi.spyOn(console, 'warn').mockImplementation(() => {});
			vi.useFakeTimers();
			try {
				const promise = apiSocket.sendSocketCommand('DefeatEnemy', { clientTotalMs: 1 });
				await flushMicrotasks();
				expect(inFlightSize(apiSocket)).toBe(1);

				// No response arrives and the socket never closes, so only the backstop can settle it.
				vi.advanceTimersByTime(30000);

				const response = await promise;
				expect(response.error).toBe('Timed out waiting for the server.');
				expect(inFlightSize(apiSocket)).toBe(0);
				expect(warnSpy).toHaveBeenCalled();
			} finally {
				vi.useRealTimers();
				warnSpy.mockRestore();
			}
		});

		it('clears the timer when a real response arrives, so a later tick never spuriously settles', async () => {
			vi.useFakeTimers();
			try {
				const promise = apiSocket.sendSocketCommand('DefeatEnemy', { clientTotalMs: 1 });
				await flushMicrotasks();
				const ws = lastWs();
				const sent = JSON.parse(ws.send.mock.calls[0][0]);

				receive(ws, JSON.stringify({ id: sent.id, name: 'DefeatEnemy', data: { cooldown: 5 } }));
				const response = await promise;
				expect(response.data).toEqual({ cooldown: 5 });
				expect(inFlightSize(apiSocket)).toBe(0);

				// Advancing well past the cap must not re-settle — the timer was cleared on resolve.
				vi.advanceTimersByTime(60000);
				expect(inFlightSize(apiSocket)).toBe(0);
			} finally {
				vi.useRealTimers();
			}
		});

		it('does not time out a queued-but-unsent command while the socket is still connecting', async () => {
			vi.useFakeTimers();
			try {
				// First socket stays CONNECTING, so the command sits queued (unsent) — no timer is armed yet.
				webSocketMock.mockImplementationOnce(function (url?: string) {
					const ws = createMockWebSocket(url);
					ws.readyState = 0;
					return ws;
				});

				const promise = apiSocket.sendSocketCommand('DefeatEnemy', { clientTotalMs: 1 });
				await flushMicrotasks();
				const connecting = lastWs();
				expect(connecting.send).not.toHaveBeenCalled();
				expect(inFlightSize(apiSocket)).toBe(0);

				// Even well past the cap, a never-sent command isn't timed out — it has no armed timer.
				vi.advanceTimersByTime(60000);
				expect(inFlightSize(apiSocket)).toBe(0);

				// Once the socket opens the command is flushed and resolves normally (onStart pings first,
				// so pick the command payload out of the send calls rather than assuming it's the first).
				connecting.readyState = connecting.OPEN;
				connecting.onopen?.();
				await flushMicrotasks();
				const sentRaw = connecting.send.mock.calls.map((c) => c[0]).find((d) => d !== 'ping' && d !== 'pong');
				const sent = JSON.parse(sentRaw);
				expect(sent.name).toBe('DefeatEnemy');
				receive(connecting, JSON.stringify({ id: sent.id, name: 'DefeatEnemy', data: { cooldown: 1 } }));
				await expect(promise).resolves.toMatchObject({ data: { cooldown: 1 } });
			} finally {
				vi.useRealTimers();
			}
		});
	});

	// The hardest orderings: a request settled by one path must never be re-settled by the other. The
	// guard is two-sided — timeoutRequest prunes the in-flight entry, and settleInFlightRequests clears
	// each request's timer — so whichever fires first leaves nothing for the other to settle again.
	describe('timeout-then-close double-settle guard', () => {
		const inFlightSize = (s: ApiSocket) =>
			(s as unknown as { inFlightRequests: Map<string, unknown> }).inFlightRequests.size;

		it('keeps the timeout error and does not re-settle when the socket later closes', async () => {
			const warnSpy = vi.spyOn(console, 'warn').mockImplementation(() => {});
			vi.useFakeTimers();
			try {
				// No tokens (localStorage cleared in beforeEach) so the later close only settles — no reconnect.
				const promise = apiSocket.sendSocketCommand('DefeatEnemy', { clientTotalMs: 1 });
				await flushMicrotasks();
				const ws = lastWs();
				expect(inFlightSize(apiSocket)).toBe(1);

				// The request times out first: it is settled with the timeout error and pruned from the map.
				vi.advanceTimersByTime(30000);
				expect(inFlightSize(apiSocket)).toBe(0);

				// A later close now finds nothing pending to settle a second time, so the promise keeps its
				// original timeout error rather than being overwritten with the connection-lost one.
				ws.readyState = ws.CLOSED;
				ws.onclose?.({ code: 1006 });

				const response = await promise;
				expect(response.error).toBe('Timed out waiting for the server.');
				expect(inFlightSize(apiSocket)).toBe(0);
			} finally {
				vi.useRealTimers();
				warnSpy.mockRestore();
			}
		});

		it('clears the request timer on close, so a later timer tick cannot re-settle it', async () => {
			const warnSpy = vi.spyOn(console, 'warn').mockImplementation(() => {});
			vi.useFakeTimers();
			try {
				const promise = apiSocket.sendSocketCommand('DefeatEnemy', { clientTotalMs: 1 });
				await flushMicrotasks();
				const ws = lastWs();
				expect(inFlightSize(apiSocket)).toBe(1);

				// The socket closes first: the in-flight request is settled with the connection-lost error and
				// its timer cleared, pruning the entry.
				ws.readyState = ws.CLOSED;
				ws.onclose?.({ code: 1006 });
				expect(inFlightSize(apiSocket)).toBe(0);

				const response = await promise;
				expect(response.error).toBe('Connection lost. Please try again.');

				// Advancing past the timeout cap must not re-settle (timer cleared) or warn (entry pruned).
				vi.advanceTimersByTime(60000);
				expect(warnSpy).not.toHaveBeenCalled();
				expect(inFlightSize(apiSocket)).toBe(0);
			} finally {
				vi.useRealTimers();
				warnSpy.mockRestore();
			}
		});
	});

	describe('listenCommand', () => {
		it('calls listener when matching command arrives', async () => {
			const listener = vi.fn();
			apiSocket.listenCommand('SocketReplaced', listener, false);

			apiSocket.sendSocketCommand('DefeatEnemy', { clientTotalMs: 1 });
			await flushMicrotasks();
			const ws = lastWs();

			receive(ws, JSON.stringify({ name: 'SocketReplaced', data: {} }));

			expect(listener).toHaveBeenCalledTimes(1);
		});

		it('supports multiple listeners for the same command', async () => {
			const listener1 = vi.fn();
			const listener2 = vi.fn();
			apiSocket.listenCommand('SocketReplaced', listener1, false);
			apiSocket.listenCommand('SocketReplaced', listener2, false);

			apiSocket.sendSocketCommand('DefeatEnemy', { clientTotalMs: 1 });
			await flushMicrotasks();
			const ws = lastWs();

			receive(ws, JSON.stringify({ name: 'SocketReplaced', data: {} }));

			expect(listener1).toHaveBeenCalledTimes(1);
			expect(listener2).toHaveBeenCalledTimes(1);
		});

		it('does not call listeners for different commands', async () => {
			const listener = vi.fn();
			apiSocket.listenCommand('SocketReplaced', listener, false);

			apiSocket.sendSocketCommand('DefeatEnemy', { clientTotalMs: 1 });
			await flushMicrotasks();
			const ws = lastWs();

			receive(ws, JSON.stringify({ id: '0', name: 'DefeatEnemy', data: {} }));

			expect(listener).not.toHaveBeenCalled();
		});
	});

	describe('ping/pong', () => {
		it('responds with pong when ping received', async () => {
			apiSocket.sendSocketCommand('DefeatEnemy', { clientTotalMs: 1 });
			await flushMicrotasks();
			const ws = lastWs();

			receive(ws, 'ping');

			expect(ws.send).toHaveBeenCalledWith('pong');
		});
	});

	describe('error handling', () => {
		it('logs error and does not crash on malformed JSON', async () => {
			const consoleSpy = vi.spyOn(console, 'error').mockImplementation(() => {});

			apiSocket.sendSocketCommand('DefeatEnemy', { clientTotalMs: 1 });
			await flushMicrotasks();
			const ws = lastWs();
			receive(ws, 'not-json');

			expect(consoleSpy).toHaveBeenCalled();
			consoleSpy.mockRestore();
		});

		it('catches listener callback errors and logs them', async () => {
			const consoleSpy = vi.spyOn(console, 'error').mockImplementation(() => {});
			const badListener = vi.fn(() => {
				throw new Error('listener error');
			});

			apiSocket.listenCommand('SocketReplaced', badListener, false);

			apiSocket.sendSocketCommand('DefeatEnemy', { clientTotalMs: 1 });
			await flushMicrotasks();
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
			await flushMicrotasks();
			const ws = lastWs();
			const sent = JSON.parse(ws.send.mock.calls[0][0]);
			expect(sent.name).toBe('GetZones');

			receive(ws, JSON.stringify({ id: sent.id, name: 'GetZones', data: [{ id: 0, name: 'Zone' }] }));

			await expect(promise).resolves.toEqual([{ id: 0, name: 'Zone' }]);
		});

		it('throws when the server reports an error', async () => {
			const promise = fetchSocketData('GetZones');
			await flushMicrotasks();
			const ws = lastWs();
			const sent = JSON.parse(ws.send.mock.calls[0][0]);

			receive(ws, JSON.stringify({ id: sent.id, name: 'GetZones', error: 'Server boom' }));

			await expect(promise).rejects.toThrow('Server boom');
		});

		it('throws when a non-error response carries no data', async () => {
			const promise = fetchSocketData('GetZones');
			await flushMicrotasks();
			const ws = lastWs();
			const sent = JSON.parse(ws.send.mock.calls[0][0]);

			// A reply with neither an error nor a payload is a protocol violation; surface it as a throw
			// rather than handing back undefined as if it were valid data.
			receive(ws, JSON.stringify({ id: sent.id, name: 'GetZones' }));

			await expect(promise).rejects.toThrow('The server returned no data.');
		});
	});

	describe('socket error propagation', () => {
		it('notifies onSocketError listeners when the socket errors', async () => {
			const consoleSpy = vi.spyOn(console, 'error').mockImplementation(() => {});
			const handler = vi.fn();
			onSocketError(handler, false);

			apiSocket.sendSocketCommand('DefeatEnemy', { clientTotalMs: 1 });
			await flushMicrotasks();
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

			apiSocket.sendSocketCommand('DefeatEnemy', { clientTotalMs: 1 });
			await flushMicrotasks();
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
			// The real refreshTokens clears storage itself on a definitive failure, so the mock mirrors that.
			vi.mocked(refreshTokens).mockImplementation(async () => {
				clearTokens();
				return null;
			});

			apiSocket.sendSocketCommand('DefeatEnemy', { clientTotalMs: 1 });
			await flushMicrotasks();
			const ws = lastWs();

			closeUnopened(ws);
			await flushMicrotasks();

			expect(refreshTokens).toHaveBeenCalledTimes(1);
			expect(handleAuthFailure).toHaveBeenCalledTimes(1);
		});

		it("recovers using a concurrent tab's rotated pair instead of routing to auth failure", async () => {
			setTokens({ accessToken: 'a', refreshToken: 'r' });
			// refreshTokens() itself gives up (mocked null), but another tab's rotated pair has since
			// landed in storage by the time the caller re-checks.
			vi.mocked(refreshTokens).mockImplementation(async () => {
				setTokens({ accessToken: 'winner-access', refreshToken: 'winner-refresh' });
				return null;
			});

			apiSocket.sendSocketCommand('DefeatEnemy', { clientTotalMs: 1 });
			await flushMicrotasks();
			const ws = lastWs();
			webSocketMock.mockClear();

			closeUnopened(ws);
			await flushMicrotasks();

			expect(refreshTokens).toHaveBeenCalledTimes(1);
			expect(handleAuthFailure).not.toHaveBeenCalled();
			// Recovers by reconnecting (processCommandQueue), same as a successful refresh.
			expect(webSocketMock).toHaveBeenCalledTimes(1);
		});

		it('does not retry a socket that had already opened', async () => {
			setTokens({ accessToken: 'a', refreshToken: 'r' });

			apiSocket.sendSocketCommand('DefeatEnemy', { clientTotalMs: 1 });
			await flushMicrotasks();
			const ws = lastWs();
			ws.onopen?.(); // marks the socket as opened

			closeUnopened(ws);
			await flushMicrotasks();

			expect(refreshTokens).not.toHaveBeenCalled();
		});

		it('does not retry on a normal closure', async () => {
			setTokens({ accessToken: 'a', refreshToken: 'r' });

			apiSocket.sendSocketCommand('DefeatEnemy', { clientTotalMs: 1 });
			await flushMicrotasks();
			const ws = lastWs();

			closeUnopened(ws, 1000);
			await flushMicrotasks();

			expect(refreshTokens).not.toHaveBeenCalled();
		});

		it('does not retry when there is no refresh token to use', async () => {
			// localStorage cleared in beforeEach → getRefreshToken() returns null.
			apiSocket.sendSocketCommand('DefeatEnemy', { clientTotalMs: 1 });
			await flushMicrotasks();
			const ws = lastWs();

			closeUnopened(ws);
			await flushMicrotasks();

			expect(refreshTokens).not.toHaveBeenCalled();
		});
	});

	describe('queued-but-unsent settlement on a terminal close', () => {
		// Queue a command against a socket that is still CONNECTING, so it sits unsent in the queue (never
		// in-flight, no timeout armed) — the exact state a connect that fails before onopen leaves behind.
		const queueOnConnectingSocket = (s: ApiSocket) => {
			webSocketMock.mockImplementationOnce(function (url?: string) {
				const ws = createMockWebSocket(url);
				ws.readyState = 0; // CONNECTING
				return ws;
			});
			return s.sendSocketCommand('DefeatEnemy', { clientTotalMs: 1 });
		};

		it('settles an unsent queued command when a normal closure arrives before it ever opened', async () => {
			setTokens({ accessToken: 'a', refreshToken: 'r' });

			const promise = queueOnConnectingSocket(apiSocket);
			await flushMicrotasks();
			const connecting = lastWs();
			expect(connecting.send).not.toHaveBeenCalled();

			connecting.readyState = connecting.CLOSED;
			connecting.onclose?.({ code: 1000 });

			const response = await promise;
			expect(response.error).toBeDefined();
		});

		it('settles an unsent queued command when the auth-retry budget is already exhausted', async () => {
			setTokens({ accessToken: 'a', refreshToken: 'r' });
			internals(apiSocket).socketAuthRetries = 5; // MAX_SOCKET_AUTH_RETRIES
			const warnSpy = vi.spyOn(console, 'warn').mockImplementation(() => {});

			const promise = queueOnConnectingSocket(apiSocket);
			await flushMicrotasks();
			const connecting = lastWs();
			expect(connecting.send).not.toHaveBeenCalled();

			connecting.readyState = connecting.CLOSED;
			connecting.onclose?.({ code: 1006 });

			const response = await promise;
			expect(response.error).toBeDefined();
			expect(refreshTokens).not.toHaveBeenCalled();
			warnSpy.mockRestore();
		});

		it('settles an unsent queued command when there is no refresh token to reconnect with', async () => {
			// localStorage cleared in beforeEach → an anonymous, token-less caller.
			const promise = queueOnConnectingSocket(apiSocket);
			await flushMicrotasks();
			const connecting = lastWs();
			expect(connecting.send).not.toHaveBeenCalled();

			connecting.readyState = connecting.CLOSED;
			connecting.onclose?.({ code: 1006 });

			const response = await promise;
			expect(response.error).toBeDefined();
			expect(refreshTokens).not.toHaveBeenCalled();
		});

		it('settles an unsent queued command when the reconnect refresh fails', async () => {
			setTokens({ accessToken: 'a', refreshToken: 'r' });
			// The real refreshTokens clears storage itself on a definitive failure, so the mock mirrors that.
			vi.mocked(refreshTokens).mockImplementation(async () => {
				clearTokens();
				return null;
			});

			const promise = queueOnConnectingSocket(apiSocket);
			await flushMicrotasks();
			const connecting = lastWs();
			expect(connecting.send).not.toHaveBeenCalled();

			connecting.readyState = connecting.CLOSED;
			connecting.onclose?.({ code: 1006 });
			await flushMicrotasks();

			const response = await promise;
			expect(response.error).toBeDefined();
			expect(handleAuthFailure).toHaveBeenCalledTimes(1);
		});
	});

	describe('listenCommand unhook', () => {
		it('returns an unhook function that removes the listener', async () => {
			const listener = vi.fn();
			const unhook = apiSocket.listenCommand('SocketReplaced', listener, false);

			apiSocket.sendSocketCommand('DefeatEnemy', { clientTotalMs: 1 });
			await flushMicrotasks();
			const ws = lastWs();

			receive(ws, JSON.stringify({ name: 'SocketReplaced', data: {} }));
			expect(listener).toHaveBeenCalledTimes(1);

			unhook();

			receive(ws, JSON.stringify({ name: 'SocketReplaced', data: {} }));
			expect(listener).toHaveBeenCalledTimes(1);
		});
	});

	describe('keepalive ping lifecycle', () => {
		it('arms the keepalive interval when a socket is created', async () => {
			setTokens({ accessToken: 'a', refreshToken: 'r' });

			apiSocket.sendSocketCommand('DefeatEnemy', { clientTotalMs: 1 });
			await flushMicrotasks();

			expect(internals(apiSocket).pingIntervalId).not.toBeNull();
		});

		it('stops the keepalive and does not reconnect once the session is gone (post-logout)', async () => {
			vi.useFakeTimers();
			try {
				setTokens({ accessToken: 'a', refreshToken: 'r' });
				apiSocket.sendSocketCommand('DefeatEnemy', { clientTotalMs: 1 });
				await flushMicrotasks();
				expect(internals(apiSocket).pingIntervalId).not.toBeNull();
				const socketsBefore = socketInstances.length;

				// Session ends (logout / unrecoverable auth failure clears the stored tokens).
				localStorage.clear();

				// The next keepalive tick must clear the interval and must not resurrect a socket.
				vi.advanceTimersByTime(10000);

				expect(internals(apiSocket).pingIntervalId).toBeNull();
				expect(socketInstances.length).toBe(socketsBefore);
			} finally {
				vi.useRealTimers();
			}
		});

		it('stops the keepalive on a normal (clean) closure', async () => {
			setTokens({ accessToken: 'a', refreshToken: 'r' });
			apiSocket.sendSocketCommand('DefeatEnemy', { clientTotalMs: 1 });
			await flushMicrotasks();
			const ws = lastWs();
			expect(internals(apiSocket).pingIntervalId).not.toBeNull();

			ws.readyState = ws.CLOSED;
			ws.onclose?.({ code: 1000 });

			expect(internals(apiSocket).pingIntervalId).toBeNull();
		});

		it('disconnect() stops the keepalive and closes the socket', async () => {
			setTokens({ accessToken: 'a', refreshToken: 'r' });
			apiSocket.sendSocketCommand('DefeatEnemy', { clientTotalMs: 1 });
			await flushMicrotasks();
			const ws = lastWs();
			expect(internals(apiSocket).pingIntervalId).not.toBeNull();

			apiSocket.disconnect();

			expect(internals(apiSocket).pingIntervalId).toBeNull();
			expect(ws.close).toHaveBeenCalledWith(1000);
		});
	});

	describe('half-open detection (missed pongs)', () => {
		it('closes the socket once MAX_MISSED_PONGS consecutive pings go unanswered', async () => {
			vi.useFakeTimers();
			try {
				setTokens({ accessToken: 'a', refreshToken: 'r' });
				apiSocket.sendSocketCommand('DefeatEnemy', { clientTotalMs: 1 });
				await flushMicrotasks();
				const ws = lastWs();

				// readyState stays OPEN throughout (the mock never flips it) — exactly the half-open case:
				// nothing ever answers the pings. 1st tick sends the first ping; 2nd–4th ticks each find the
				// prior ping still unanswered (3 consecutive misses), so the 4th tick closes the socket.
				vi.advanceTimersByTime(40000);

				expect(ws.close).toHaveBeenCalled();
			} finally {
				vi.useRealTimers();
			}
		});

		it('does not close the socket while every ping keeps getting answered', async () => {
			vi.useFakeTimers();
			try {
				setTokens({ accessToken: 'a', refreshToken: 'r' });
				apiSocket.sendSocketCommand('DefeatEnemy', { clientTotalMs: 1 });
				await flushMicrotasks();
				const ws = lastWs();

				for (let i = 0; i < 5; i++) {
					vi.advanceTimersByTime(10000);
					receive(ws, 'pong');
				}

				expect(ws.close).not.toHaveBeenCalled();
			} finally {
				vi.useRealTimers();
			}
		});

		it('a pong resets the missed-pong streak, so an isolated slow round trip does not close the socket', async () => {
			vi.useFakeTimers();
			try {
				setTokens({ accessToken: 'a', refreshToken: 'r' });
				apiSocket.sendSocketCommand('DefeatEnemy', { clientTotalMs: 1 });
				await flushMicrotasks();
				const ws = lastWs();

				// Two ticks with no pong (one miss shy of the threshold), then a late pong arrives and
				// resets the streak before it can accumulate further.
				vi.advanceTimersByTime(20000);
				receive(ws, 'pong');

				// Two more unanswered ticks after the reset stay well under the threshold.
				vi.advanceTimersByTime(20000);

				expect(ws.close).not.toHaveBeenCalled();
			} finally {
				vi.useRealTimers();
			}
		});
	});

	describe('bounded auth retries', () => {
		// Reject a handshake before it ever opens (the auth-rejection signature), without calling onopen.
		const rejectHandshake = (ws: MockWebSocket, code = 1006) => {
			ws.readyState = ws.CLOSED;
			ws.onclose?.({ code });
		};

		it('stops refreshing after the retry limit when no reconnect ever becomes stable', async () => {
			setTokens({ accessToken: 'a', refreshToken: 'r' });
			vi.mocked(refreshTokens).mockResolvedValue({ accessToken: 'a2', refreshToken: 'r2' });
			const warnSpy = vi.spyOn(console, 'warn').mockImplementation(() => {});

			apiSocket.sendSocketCommand('DefeatEnemy', { clientTotalMs: 1 });
			await flushMicrotasks();

			// Each reconnect is rejected pre-open again; without a stable open the budget is never refilled.
			for (let i = 0; i < 10; i++) {
				rejectHandshake(lastWs());
				await flushMicrotasks();
			}

			// Bounded at MAX_SOCKET_AUTH_RETRIES (5), not once per rejection — no unbounded refresh storm.
			expect(refreshTokens).toHaveBeenCalledTimes(5);
			expect(warnSpy).toHaveBeenCalled();
			warnSpy.mockRestore();
		});

		it('stops the keepalive and routes to re-auth when the retry budget is exhausted', async () => {
			vi.useFakeTimers();
			const warnSpy = vi.spyOn(console, 'warn').mockImplementation(() => {});
			try {
				setTokens({ accessToken: 'a', refreshToken: 'r' });
				vi.mocked(refreshTokens).mockResolvedValue({ accessToken: 'a2', refreshToken: 'r2' });

				apiSocket.sendSocketCommand('DefeatEnemy', { clientTotalMs: 1 });
				await flushMicrotasks();
				expect(internals(apiSocket).pingIntervalId).not.toBeNull();

				// Reject every reconnect pre-open so the budget is spent and the next close hits the cap.
				for (let i = 0; i < 6; i++) {
					rejectHandshake(lastWs());
					await flushMicrotasks();
				}

				// Exhausting the budget is terminal: the keepalive is torn down and the user is routed to
				// re-auth, rather than being left to silently reconnect.
				expect(internals(apiSocket).pingIntervalId).toBeNull();
				expect(handleAuthFailure).toHaveBeenCalled();

				// With the interval cleared, the keepalive can no longer fire to resurrect a socket and
				// bypass the bound: advancing past the ping cadence opens no new connection.
				const socketsBefore = socketInstances.length;
				await vi.advanceTimersByTimeAsync(30000);
				expect(socketInstances.length).toBe(socketsBefore);
			} finally {
				vi.useRealTimers();
				warnSpy.mockRestore();
			}
		});

		it('refills the retry budget only after the connection stays open (stable), not on open', async () => {
			vi.useFakeTimers();
			try {
				setTokens({ accessToken: 'a', refreshToken: 'r' });
				internals(apiSocket).socketAuthRetries = 3;

				apiSocket.sendSocketCommand('DefeatEnemy', { clientTotalMs: 1 });
				await flushMicrotasks();
				const ws = lastWs();
				ws.onopen?.();
				// Opening alone does not refill the budget — it only arms the stability timer.
				expect(internals(apiSocket).socketAuthRetries).toBe(3);

				// Staying open past the stability window refills it.
				vi.advanceTimersByTime(10000);
				expect(internals(apiSocket).socketAuthRetries).toBe(0);
			} finally {
				vi.useRealTimers();
			}
		});

		it('does not refill the retry budget when the socket closes before becoming stable', async () => {
			vi.useFakeTimers();
			try {
				setTokens({ accessToken: 'a', refreshToken: 'r' });
				internals(apiSocket).socketAuthRetries = 3;

				apiSocket.sendSocketCommand('DefeatEnemy', { clientTotalMs: 1 });
				await flushMicrotasks();
				const ws = lastWs();
				ws.onopen?.(); // arms the stability timer
				ws.readyState = ws.CLOSED;
				ws.onclose?.({ code: 1006 }); // closes before the window elapses → cancels the refill

				vi.advanceTimersByTime(20000);
				expect(internals(apiSocket).socketAuthRetries).toBe(3);
			} finally {
				vi.useRealTimers();
			}
		});

		it('arms the stability timer on open and clears it on a pre-stable close (the flap-refill guard)', async () => {
			// Read the refill timer handle directly: the budget-stability tests above assert the counter
			// side-effect, but the flap guard the issue calls out is the connectionStableTimer being armed
			// in onStart and torn down in handleClose so a brief open can't schedule a stale refill.
			const stableTimer = (s: ApiSocket) =>
				(s as unknown as { connectionStableTimer: ReturnType<typeof setTimeout> | null }).connectionStableTimer;

			setTokens({ accessToken: 'a', refreshToken: 'r' });
			apiSocket.sendSocketCommand('DefeatEnemy', { clientTotalMs: 1 });
			await flushMicrotasks();
			const ws = lastWs();

			ws.onopen?.(); // onStart arms the "connection is now stable" refill timer
			expect(stableTimer(apiSocket)).not.toBeNull();

			// A flap (closes before the stability window elapses) cancels the pending refill.
			ws.readyState = ws.CLOSED;
			ws.onclose?.({ code: 1006 });
			expect(stableTimer(apiSocket)).toBeNull();
		});
	});
});
