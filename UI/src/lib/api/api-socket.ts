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
import { handleAuthFailure, refreshTokens } from './auth';

/** WebSocket close code for a clean, intentional shutdown — never an auth failure. */
const NORMAL_CLOSURE = 1000;

/** Keepalive / latency-probe cadence. Also doubles as a background reconnect for an idle session whose
 *  socket dropped, so it is deliberately torn down (not just left firing) once the session ends. */
const PING_INTERVAL_MS = 10000;

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

export interface IApiSocketResponse<T extends ApiSocketCommand | void = void> {
	id: string;
	name: T;
	error?: string;
	data: T extends ApiSocketCommand ? ApiSocketResponseTypes[T] : never;
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

	private ensureSocket() {
		if (!socket || socket.readyState === socket.CLOSED) {
			this.socketOpened = false;
			// Browsers can't set an Authorization header on the WebSocket handshake, so the access token
			// is passed as a query-string parameter (the standard ASP.NET Core token-over-WS pattern).
			const accessToken = getAccessToken();
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
		this.processCommandQueue();
		return await request.getResponse();
	}

	public listenCommand<T extends ApiSocketCommand>(
		commandName: T,
		action: Action<[IApiSocketResponse<T>]>,
		cleanupOnDestroy: boolean = true
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
		this.ensureSocket();
		if (socket && socket.readyState === socket.OPEN) {
			this.lastPing = performance.now();
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

	private processCommandQueue() {
		this.ensureSocket();
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
		// an error so the awaiting caller surfaces a toast / retries instead of hanging forever. Queued-but-
		// unsent commands are untouched, so the auth-retry path below still re-sends them on reconnect.
		this.settleInFlightRequests('Connection lost. Please try again.');

		// The socket didn't stay open, so cancel any pending "connection is now stable" budget refill.
		this.clearConnectionStableTimer();

		if (ev.code === NORMAL_CLOSURE) {
			// A clean, intentional close (e.g. an explicit disconnect or a server-side graceful shutdown):
			// stop the keepalive rather than reconnecting. A later user-initiated command re-arms it via
			// ensureSocket.
			this.stopPingInterval();
			return;
		}

		if (this.socketOpened) {
			return;
		}

		if (this.socketAuthRetries >= MAX_SOCKET_AUTH_RETRIES) {
			console.warn(`Socket auth retry limit (${MAX_SOCKET_AUTH_RETRIES}) reached; not refreshing again.`);
			return;
		}

		if (!getRefreshToken()) {
			return;
		}

		this.socketAuthRetries++;
		refreshTokens().then((tokens) => {
			if (tokens) {
				this.ensureSocket();
				this.processCommandQueue();
			} else {
				// Refresh is spent/revoked — the session is unrecoverable. Stop the keepalive before routing
				// to login so it can't fire a reconnect on the now-cleared token during the teardown.
				this.stopPingInterval();
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
		this.processCommandQueue();
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
	if (response.error) {
		throw new Error(response.error);
	}
	return response.data;
}
