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
	private socketAuthRetried = false;
	private pingIntervalStarted = false;

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
			if (!this.pingIntervalStarted) {
				this.pingIntervalStarted = true;
				setInterval(() => apiSocket.attemptPing(), 10000);
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
		this.ensureSocket();
		if (socket && socket.readyState === socket.OPEN) {
			this.lastPing = performance.now();
			socket.send('ping');
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
				this.inFlightRequests.set(request.id, { startTime: performance.now(), command: request });
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
			if (hook) {
				try {
					hook.notify(data);
				} catch (ex) {
					console.error('An error occurred while executing a socket listener callback', ex);
				}
			}

			if (data.id) {
				const inFlight = this.inFlightRequests.get(data.id);
				if (inFlight) {
					// Prune as soon as it resolves so the map can't grow without bound over a long session.
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
		for (const { command } of this.inFlightRequests.values()) {
			command.settleWithError(error);
		}
		this.inFlightRequests.clear();
	}

	private handleError(ev: Event) {
		console.error('A socket error occurred', ev);
		errorHook.notify('WebSocket connection error');
	}

	/**
	 * A handshake rejected for auth reasons (expired/invalid access token) closes the socket without it
	 * ever opening. When that happens we refresh the token pair once and reconnect. A socket that did
	 * open before dropping (e.g. SocketReplaced, or a transient network blip) is left alone — its own
	 * handler manages the teardown, and retrying here risks fighting an intentional close or looping
	 * against a server that keeps closing the connection.
	 */
	private handleClose(ev: CloseEvent) {
		// A command sent before the drop will never get a response on this dead socket, and we deliberately
		// don't blind-resend (commands like DefeatEnemy aren't idempotent). Settle each pending request with
		// an error so the awaiting caller surfaces a toast / retries instead of hanging forever. Queued-but-
		// unsent commands are untouched, so the auth-retry path below still re-sends them on reconnect.
		this.settleInFlightRequests('Connection lost. Please try again.');

		if (this.socketOpened || this.socketAuthRetried || ev.code === NORMAL_CLOSURE) {
			return;
		}

		if (!getRefreshToken()) {
			return;
		}

		this.socketAuthRetried = true;
		refreshTokens().then((tokens) => {
			if (tokens) {
				this.ensureSocket();
				this.processCommandQueue();
			} else {
				handleAuthFailure();
			}
		});
	}

	private onStart() {
		this.socketOpened = true;
		this.socketAuthRetried = false;
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
