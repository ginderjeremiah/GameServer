import {
	ApiSocketCommand,
	ApiSocketResponseTypes,
	ApiSocketCommandNoRequest,
	ApiSocketCommandWithRequest,
	ApiSocketRequestTypes
} from './types/api-socket-type-map';
import { ApiSocketRequest } from './api-socket-request';
import { createHook } from '../common/hooks';
import { Action } from '../common/types';
import { getAccessToken, getRefreshToken } from './token-store';
import { ensureValidAccessToken, handleAuthFailure, refreshTokens } from './auth';

/** WebSocket close code for a clean, intentional shutdown — never an auth failure. */
const NORMAL_CLOSURE = 1000;

/** Keepalive / latency-probe cadence. Also doubles as a background reconnect for an idle session whose
 *  socket dropped, so it is deliberately torn down (not just left firing) once the session ends. */
const PING_INTERVAL_MS = 10000;

/** A `readyState` of OPEN doesn't mean the connection is alive — after a laptop sleep/wake or a NAT drop
 *  the browser can be unaware the underlying TCP connection is dead for minutes. This many consecutive
 *  pings that never get a pong back is treated as proof the socket is half-open (not just a single slow
 *  round trip), at which point it's closed so the keepalive's own reconnect logic takes over. */
const MAX_MISSED_PONGS = 3;

/** A handshake auth-rejection refreshes the token pair and reconnects, but only this many times in a row
 *  before giving up — so a server that keeps rejecting a freshly-refreshed token (a flapping or broken
 *  server) can't drive an unbounded refresh/reconnect loop that rapidly burns single-use refresh tokens. */
const MAX_SOCKET_AUTH_RETRIES = 5;

/** The auth-retry budget above is refilled only once a reconnected socket has stayed open at least this
 *  long, never the instant it opens — so a socket that opens then is immediately closed (a post-open
 *  rejection / flap) can't reset the count every cycle and re-enable the loop. A healthy connection
 *  trivially clears this bar and regains a full budget for a genuine future re-auth. */
const STABLE_CONNECTION_MS = 10000;

/** Per-request backstop for the case the close-handler can't cover: the socket stays open but a
 *  response simply never arrives (e.g. the server processed the command but failed to send a reply,
 *  or sent one that failed to parse). Without it the in-flight entry — and the caller's await — would
 *  sit forever. Kept generous so legitimately-slow commands (e.g. a heavy admin reference read) aren't
 *  spuriously failed; a real reply normally lands in well under a second. */
const REQUEST_TIMEOUT_MS = 30000;

/** Surfaced via the resolve-with-error contract when a sent request exceeds REQUEST_TIMEOUT_MS. */
const REQUEST_TIMEOUT_ERROR = 'Timed out waiting for the server.';

/** Surfaced via the resolve-with-error contract when the socket drops (or closes for good) before a
 *  pending command could be answered — both in-flight commands and queued-but-unsent ones on a close
 *  that won't reconnect, so neither leaves its caller awaiting a response that will never come. */
const CONNECTION_LOST_ERROR = 'Connection lost. Please try again.';

export interface IApiSocketResponse<T extends ApiSocketCommand | void = void> {
	id: string;
	name: T;
	error?: string;
	// Optional because every failure (server error, dropped connection, timeout) resolves with an
	// `error` and no `data`; the type then forces callers to guard `error`/optional-chain `data`.
	data?: T extends ApiSocketCommand ? ApiSocketResponseTypes[T] : never;
}

let socket: WebSocket;

const errorHook = createHook<[string]>();
export const onSocketError = errorHook.onNotified;

const pingHook = createHook<[number]>();
export const onPingMeasured = pingHook.onNotified;

type InFlightRequest = {
	startTime: number;
	// Cleared when the response resolves; fires the timeout backstop if it never does.
	timer: ReturnType<typeof setTimeout>;
	// eslint-disable-next-line @typescript-eslint/no-explicit-any -- heterogeneous queue of requests for differing commands; ApiSocketRequest<T> is invariant in T so a common supertype isn't expressible.
	command: ApiSocketRequest<any>;
};

export class ApiSocket {
	// eslint-disable-next-line @typescript-eslint/no-explicit-any -- heterogeneous queue of requests for differing commands; ApiSocketRequest<T> is invariant in T so a common supertype isn't expressible.
	private socketCommandQueue: ApiSocketRequest<any>[] = [];
	// Keyed by command id so a response is an O(1) lookup and a settled request can be pruned (rather
	// than scanned for and left in an ever-growing array over a long, chatty session).
	private inFlightRequests = new Map<string, InFlightRequest>();
	private commandCounter = 0;
	private commandHooks: Partial<{
		[key in ApiSocketCommand]: ReturnType<typeof createHook<[IApiSocketResponse<key>]>>;
	}> = {};
	private lastPing = 0;
	// Set when a ping is sent, cleared when its pong arrives; consecutive misses drive the half-open
	// detection in attemptPing (see MAX_MISSED_PONGS).
	private awaitingPong = false;
	private missedPongs = 0;
	private socketOpened = false;
	// Consecutive handshake-auth refresh/reconnect attempts since the last *stable* open; bounded by
	// MAX_SOCKET_AUTH_RETRIES so a flapping/post-open-rejecting server can't loop and burn refresh tokens.
	private socketAuthRetries = 0;
	// The keepalive interval handle (null = not running) so it can be cleared on a clean close, an
	// unrecoverable auth failure, or an explicit disconnect — rather than firing for the lifetime of the
	// module singleton and trying to resurrect a socket for a logged-out user.
	private pingIntervalId: ReturnType<typeof setInterval> | null = null;
	// Pending "the connection has proven stable" timer that refills the auth-retry budget; cleared if the
	// socket closes before it fires (so a brief open doesn't reset the count mid-loop).
	private connectionStableTimer: ReturnType<typeof setTimeout> | null = null;
	// Single-flight guard for the async open path: the in-flight open promise (null when not connecting),
	// shared by concurrent ensureSocket callers so the pre-emptive-refresh await can't race two opens.
	private connecting: Promise<void> | null = null;

	/**
	 * Ensures a live (or still-connecting) socket exists, opening one if needed. The open path is async
	 * because it pre-emptively refreshes the access token first (see openSocket), so concurrent callers
	 * are collapsed onto a single in-flight open via the `connecting` guard — otherwise a keepalive ping
	 * and a queued command could each pass the "is there a socket?" check during the refresh await and
	 * race to create (and leak) two sockets.
	 */
	private ensureSocket(): Promise<void> {
		if (socket && socket.readyState !== socket.CLOSED) {
			return Promise.resolve();
		}
		if (this.connecting) {
			return this.connecting;
		}
		this.connecting = this.openSocket().finally(() => {
			this.connecting = null;
		});
		return this.connecting;
	}

	private async openSocket(): Promise<void> {
		this.socketOpened = false;
		this.awaitingPong = false;
		this.missedPongs = 0;
		// Pre-emptively refresh an access token that is missing or about to expire (mirroring the HTTP
		// path) so a reconnect doesn't hand the server a stale token, eat a rejected handshake, and burn a
		// single-use refresh token recovering in handleClose.
		const hadRefreshToken = getRefreshToken() !== null;
		const accessToken = await ensureValidAccessToken();
		// A logged-in session whose pre-emptive refresh failed (refresh token spent/revoked) is
		// unrecoverable: route to the auth-failure handler rather than opening a doomed handshake the close
		// handler can no longer recover from (its refresh token is now gone). A never-logged-in caller
		// (no prior refresh token) still opens an unauthenticated socket below.
		if (!accessToken && hadRefreshToken) {
			this.stopPingInterval();
			handleAuthFailure();
			return;
		}
		// Browsers can't set an Authorization header on the WebSocket handshake, so the access token
		// is passed as a query-string parameter (the standard ASP.NET Core token-over-WS pattern).
		const url = accessToken ? `/socket?access_token=${encodeURIComponent(accessToken)}` : '/socket';
		socket = new WebSocket(url);
		socket.onopen = this.onStart.bind(this);
		socket.onmessage = this.receiveResponse.bind(this);
		socket.onerror = this.handleError.bind(this);
		socket.onclose = this.handleClose.bind(this);
		if (this.pingIntervalId === null) {
			this.pingIntervalId = setInterval(() => this.attemptPing(), PING_INTERVAL_MS);
		}
	}

	public async sendSocketCommand<T extends ApiSocketCommandNoRequest>(commandName: T): Promise<IApiSocketResponse<T>>;
	public async sendSocketCommand<T extends ApiSocketCommandWithRequest>(
		commandName: T,
		params: ApiSocketRequestTypes[T]
	): Promise<IApiSocketResponse<T>>;
	// eslint-disable-next-line @typescript-eslint/no-explicit-any -- implementation signature behind the typed overloads above; TS cannot narrow the conditional param type from the generic T here.
	public async sendSocketCommand<T extends ApiSocketCommand>(commandName: T, params?: any) {
		const id = (this.commandCounter++).toString();
		const request = new ApiSocketRequest(id, commandName, params);
		this.socketCommandQueue.push(request);
		// Fire-and-forget: the socket-open path is async (pre-emptive refresh), so the command is sent once
		// the socket is ready; the caller awaits the response below, not the connection.
		void this.processCommandQueue();
		return await request.getResponse();
	}

	/**
	 * Subscribes to a server-pushed command and returns an unsubscribe function. Mirrors the underlying
	 * hook's lifecycle contract: `cleanupOnDestroy` defaults off, so only pass `true` from within Svelte
	 * component init; callers outside it must call the returned unsubscribe themselves.
	 */
	public listenCommand<T extends ApiSocketCommand>(
		commandName: T,
		action: Action<[IApiSocketResponse<T>]>,
		cleanupOnDestroy: boolean = false
	) {
		const hook = this.getOrCreateHook(commandName);
		return hook.onNotified((data: IApiSocketResponse<T>) => action(data), cleanupOnDestroy);
	}

	public attemptPing() {
		// The keepalive must not resurrect a socket for a session that has ended: once the tokens are gone
		// (logout / unrecoverable auth failure) stop the interval instead of reconnecting on a cleared
		// token, which would otherwise produce post-logout reconnect noise.
		if (!getAccessToken()) {
			this.stopPingInterval();
			return;
		}
		void this.ensureSocket();
		if (socket && socket.readyState === socket.OPEN) {
			if (this.awaitingPong) {
				this.missedPongs++;
				if (this.missedPongs >= MAX_MISSED_PONGS) {
					// readyState alone can't tell a half-open socket from a live one; MAX_MISSED_PONGS
					// consecutive silent pings is the signal. Close it so handleClose's existing
					// reconnect machinery takes over, rather than leaving every command to eat the full
					// per-request timeout until the OS notices the connection is dead.
					console.warn(`Socket missed ${this.missedPongs} consecutive pongs; closing the half-open connection.`);
					this.awaitingPong = false;
					this.missedPongs = 0;
					// Deliberately code-less: handleClose only stops the keepalive on ev.code ===
					// NORMAL_CLOSURE, so this must surface as 1005/1006 to fall through to its
					// reconnect branch. Passing NORMAL_CLOSURE here would silently disable auto-reconnect.
					socket.close();
					return;
				}
			}
			this.lastPing = performance.now();
			this.awaitingPong = true;
			socket.send('ping');
		}
	}

	/**
	 * Fully tears down the live connection for a session that is ending without a full-page reload (the
	 * SocketReplaced handler navigates client-side, so the module singleton — and this interval — survive):
	 * stops the keepalive ping and closes the socket so a background ping can't silently reconnect and, in
	 * the SocketReplaced case, fight the session that just took over.
	 */
	public disconnect() {
		this.stopPingInterval();
		this.clearConnectionStableTimer();
		if (socket && socket.readyState !== socket.CLOSED) {
			socket.close(NORMAL_CLOSURE);
		}
	}

	private stopPingInterval() {
		if (this.pingIntervalId !== null) {
			clearInterval(this.pingIntervalId);
			this.pingIntervalId = null;
		}
	}

	private clearConnectionStableTimer() {
		if (this.connectionStableTimer !== null) {
			clearTimeout(this.connectionStableTimer);
			this.connectionStableTimer = null;
		}
	}

	private getOrCreateHook<T extends ApiSocketCommand>(commandName: T) {
		const hook = this.commandHooks[commandName];
		if (!hook) {
			const newHook = createHook<[IApiSocketResponse<T>]>();
			(this.commandHooks as Record<T, typeof newHook>)[commandName] = newHook;
			return newHook;
		}

		return hook;
	}

	private async processCommandQueue() {
		await this.ensureSocket();
		if (socket && socket.readyState === socket.OPEN) {
			let request: ApiSocketRequest | undefined;
			while ((request = this.socketCommandQueue.shift())) {
				// Arm the timeout only now that the command is actually sent — a command can sit queued
				// during a reconnect, and that wait shouldn't count against its per-request budget.
				const id = request.id;
				const timer = setTimeout(() => this.timeoutRequest(id), REQUEST_TIMEOUT_MS);
				this.inFlightRequests.set(id, { startTime: performance.now(), timer, command: request });
				socket.send(JSON.stringify(request.getCommandInfo()));
			}
		}
	}

	private receiveResponse(ev: MessageEvent) {
		const now = performance.now();
		if (ev.data == 'pong') {
			this.awaitingPong = false;
			this.missedPongs = 0;
			pingHook.notify(now - this.lastPing);
			return;
		} else if (ev.data === 'ping') {
			socket.send('pong');
			return;
		}

		try {
			const data = JSON.parse(ev.data) as IApiSocketResponse<ApiSocketCommand>;
			const hook = this.getOrCreateHook(data.name);
			try {
				hook.notify(data);
			} catch (ex) {
				console.error('An error occurred while executing a socket listener callback', ex);
			}

			if (data.id) {
				const inFlight = this.inFlightRequests.get(data.id);
				if (inFlight) {
					// Prune as soon as it resolves so the map can't grow without bound over a long session,
					// and clear its timeout so the backstop can't later settle an already-resolved request.
					clearTimeout(inFlight.timer);
					this.inFlightRequests.delete(data.id);
					inFlight.command.resolve(data);
					console.debug(`Response to '${inFlight.command.commandName}' received after ${now - inFlight.startTime}ms.`);
				}
			}
		} catch (ex) {
			console.error('Failed to handle socket response', ex);
		}
	}

	/** Resolves every still-pending in-flight request with an error response and clears the tracking map.
	 *  Reuses the transport's resolve-with-error contract (rather than rejecting) so existing callers handle
	 *  a dropped connection through the same `response.error` path they already use for server errors. */
	private settleInFlightRequests(error: string) {
		for (const { command, timer } of this.inFlightRequests.values()) {
			clearTimeout(timer);
			command.settleWithError(error);
		}
		this.inFlightRequests.clear();
	}

	/** Resolves every queued-but-unsent command with an error response and empties the queue. These
	 *  commands were never sent (so they're not in the in-flight map) and never had a timeout armed, so on
	 *  a close that won't reconnect and re-flush them they'd otherwise await a response forever. Only the
	 *  reconnecting paths (the keepalive after a post-open drop, the auth-retry after a refresh) keep the
	 *  queue to re-send it; every terminal close settles it here. */
	private settleQueuedRequests(error: string) {
		let request: ApiSocketRequest | undefined;
		while ((request = this.socketCommandQueue.shift())) {
			request.settleWithError(error);
		}
	}

	/** Backstop for a request that was sent but whose response never arrived while the socket stayed
	 *  open (so the close-handler never settled it). Settles it through the same resolve-with-error
	 *  contract and prunes the entry, so the caller surfaces it via `response.error` rather than
	 *  awaiting forever. A no-op if the request already resolved (its timer would have been cleared). */
	private timeoutRequest(id: string) {
		const inFlight = this.inFlightRequests.get(id);
		if (!inFlight) {
			return;
		}
		this.inFlightRequests.delete(id);
		inFlight.command.settleWithError(REQUEST_TIMEOUT_ERROR);
		console.warn(`Request '${inFlight.command.commandName}' timed out after ${REQUEST_TIMEOUT_MS}ms.`);
	}

	private handleError(ev: Event) {
		console.error('A socket error occurred', ev);
		errorHook.notify('WebSocket connection error');
	}

	/**
	 * A handshake rejected for auth reasons (expired/invalid access token) closes the socket without it
	 * ever opening. When that happens we refresh the token pair and reconnect — but only up to
	 * MAX_SOCKET_AUTH_RETRIES times in a row, so a server that keeps rejecting a freshly-refreshed token
	 * can't loop and burn rotating refresh tokens; the budget is refilled once a reconnect proves stable
	 * (see onStart). A socket that did open before dropping (SocketReplaced, or a transient network blip)
	 * is left for the keepalive ping to reconnect — refreshing wouldn't help, and retrying here risks
	 * fighting an intentional close.
	 */
	private handleClose(ev: CloseEvent) {
		// A command sent before the drop will never get a response on this dead socket, and we deliberately
		// don't blind-resend (commands like DefeatEnemy aren't idempotent). Settle each pending request with
		// an error so the awaiting caller surfaces a toast / retries instead of hanging forever.
		this.settleInFlightRequests(CONNECTION_LOST_ERROR);

		// The socket didn't stay open, so cancel any pending "connection is now stable" budget refill.
		this.clearConnectionStableTimer();

		if (ev.code === NORMAL_CLOSURE) {
			// A clean, intentional close (e.g. an explicit disconnect or a server-side graceful shutdown):
			// stop the keepalive rather than reconnecting. A later user-initiated command re-arms it via
			// ensureSocket. Nothing will re-flush the queue, so settle any unsent commands instead of
			// stranding them (a NORMAL_CLOSURE received before the socket ever opened never sent them).
			this.stopPingInterval();
			this.settleQueuedRequests(CONNECTION_LOST_ERROR);
			return;
		}

		if (this.socketOpened) {
			// A socket that opened before dropping flushed its queue in onStart; the keepalive ping will
			// reconnect and re-flush anything queued since, so leave the queue for it to re-send.
			return;
		}

		if (this.socketAuthRetries >= MAX_SOCKET_AUTH_RETRIES) {
			// Budget exhausted: repeated freshly-refreshed tokens are still being rejected at the handshake,
			// so the session is effectively unrecoverable. We won't reconnect, so settle the unsent queue
			// rather than leaving it to await a response that never comes, then stop the keepalive and route
			// to re-auth (mirroring the refresh-failure branch below) — otherwise the ping would keep calling
			// ensureSocket on the still-present access token, reconnecting via a path that never increments
			// socketAuthRetries and so bypassing this very bound.
			console.warn(`Socket auth retry limit (${MAX_SOCKET_AUTH_RETRIES}) reached; not refreshing again.`);
			this.settleQueuedRequests(CONNECTION_LOST_ERROR);
			this.stopPingInterval();
			handleAuthFailure();
			return;
		}

		if (!getRefreshToken()) {
			// No refresh token to recover with (anonymous / token-less caller): the handshake won't be
			// retried, so settle the unsent queue here too.
			this.settleQueuedRequests(CONNECTION_LOST_ERROR);
			return;
		}

		this.socketAuthRetries++;
		refreshTokens().then((tokens) => {
			if (tokens) {
				// processCommandQueue ensures the socket (now with the freshly refreshed token) and flushes
				// any queued-but-unsent commands.
				void this.processCommandQueue();
			} else {
				// Refresh is spent/revoked — the session is unrecoverable. Stop the keepalive before routing
				// to login so it can't fire a reconnect on the now-cleared token during the teardown, and
				// settle the unsent queue since nothing will re-flush it.
				this.stopPingInterval();
				this.settleQueuedRequests(CONNECTION_LOST_ERROR);
				handleAuthFailure();
			}
		});
	}

	private onStart() {
		this.socketOpened = true;
		// Refill the auth-retry budget only after the connection proves stable (stays open a while), not
		// the instant it opens — otherwise a socket that opens then is immediately closed would reset the
		// count every cycle and let the refresh/reconnect loop run unbounded against a flapping server.
		this.clearConnectionStableTimer();
		this.connectionStableTimer = setTimeout(() => {
			this.socketAuthRetries = 0;
			this.connectionStableTimer = null;
		}, STABLE_CONNECTION_MS);
		this.attemptPing();
		void this.processCommandQueue();
	}
}

export const apiSocket = new ApiSocket();

/**
 * Sends a no-request socket command and returns its data, throwing if the server reported an error.
 * Mirrors the throw-on-error contract of `ApiRequest.get` (the socket transport otherwise resolves
 * with an `error` field rather than rejecting), so socket-backed callers such as the admin Workbench
 * surface failures the same way the HTTP client did instead of silently treating an error as success.
 */
export async function fetchSocketData<T extends ApiSocketCommandNoRequest>(
	command: T
): Promise<ApiSocketResponseTypes[T]> {
	const response = await apiSocket.sendSocketCommand(command);
	// A failure resolves with an `error` and no `data`; a successful reply always carries data, so a
	// missing payload on a non-error response is itself a protocol error worth surfacing as a throw.
	if (response.error || response.data === undefined) {
		throw new Error(response.error ?? 'The server returned no data.');
	}
	return response.data;
}
