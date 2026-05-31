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
	command: ApiSocketRequest<any>;
};

export class ApiSocket {
	private socketCommandQueue: ApiSocketRequest<any>[] = [];
	private inFlightRequests: InFlightRequest[] = [];
	private commandCounter = 0;
	private commandHooks: Partial<{
		[key in ApiSocketCommand]: ReturnType<typeof createHook<[IApiSocketResponse<key>]>>;
	}> = {};
	private lastPing = 0;

	private ensureSocket() {
		if (!socket || socket.readyState === socket.CLOSED) {
			socket = new WebSocket('/socket');
			socket.onopen = this.onStart.bind(this);
			socket.onmessage = this.receiveResponse.bind(this);
			socket.onerror = this.handleError.bind(this);
			setInterval(() => apiSocket.attemptPing(), 10000);
		}
	}

	public async sendSocketCommand<T extends ApiSocketCommandNoRequest>(commandName: T): Promise<IApiSocketResponse<T>>;
	public async sendSocketCommand<T extends ApiSocketCommandWithRequest>(
		commandName: T,
		params: ApiSocketRequestTypes[T]
	): Promise<IApiSocketResponse<T>>;
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
		if (socket.readyState === socket.OPEN) {
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
		if (socket.readyState === socket.OPEN) {
			let request: ApiSocketRequest | undefined;
			while ((request = this.socketCommandQueue.shift())) {
				this.inFlightRequests.push({ startTime: performance.now(), command: request });
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
				const request = this.inFlightRequests.find((c) => c.command.id === data.id);
				if (request) {
					request.command.resolve(data);
					console.debug(`Response to '${request.command.commandName}' received after ${request.startTime - now}ms.`);
				}
			}
		} catch (ex) {
			console.error('Failed to handle socket response', ex);
		}
	}

	private handleError(ev: Event) {
		console.error('A socket error occurred', ev);
		errorHook.notify('WebSocket connection error');
	}

	private onStart() {
		this.attemptPing();
		this.processCommandQueue();
	}
}

export const apiSocket = new ApiSocket();
