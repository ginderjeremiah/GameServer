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

export class ApiSocket {
	private socketCommandQueue: ApiSocketRequest<any>[] = [];
	private inFlightCommands: ApiSocketRequest<any>[] = [];
	private commandCounter = 0;
	private commandHooks: Partial<{
		[key in ApiSocketCommand]: ReturnType<typeof createHook<[IApiSocketResponse<key>]>>;
	}> = {};

	private ensureSocket() {
		if (!socket || socket.readyState === socket.CLOSED) {
			socket = new WebSocket('/socket');
			socket.onopen = this.processCommandQueue.bind(this);
			socket.onmessage = this.receiveResponse.bind(this);
			socket.onerror = this.handleError.bind(this);
		}
	}

	public async sendSocketCommand<T extends ApiSocketCommandNoRequest>(
		commandName: T
	): Promise<IApiSocketResponse<T>>;
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
		return hook.onNotified(
			(data: IApiSocketResponse<T>) => action(data),
			cleanupOnDestroy
		);
	}

	private getOrCreateHook<T extends ApiSocketCommand>(commandName: T) {
		if (!this.commandHooks[commandName]) {
			(this.commandHooks as any)[commandName] = createHook<[IApiSocketResponse<T>]>();
		}
		return this.commandHooks[commandName] as ReturnType<
			typeof createHook<[IApiSocketResponse<T>]>
		>;
	}

	private processCommandQueue() {
		this.ensureSocket();
		if (socket.readyState === socket.OPEN) {
			let request: ApiSocketRequest | undefined;
			while ((request = this.socketCommandQueue.shift())) {
				this.inFlightCommands.push(request);
				socket.send(JSON.stringify(request.getCommandInfo()));
			}
		}
	}

	private receiveResponse(ev: MessageEvent) {
		if (ev.data === 'ping') {
			socket.send('pong');
		} else {
			try {
				const data = JSON.parse(ev.data) as IApiSocketResponse<ApiSocketCommand>;
				const hook = this.commandHooks[data.name];
				if (hook) {
					try {
						hook.notify(data as any);
					} catch (ex) {
						console.error('An error occurred while executing a socket listener callback', ex);
					}
				}

				if (data.id) {
					const response = this.inFlightCommands.find((c) => c.id === data.id);
					response?.resolve(data);
				}
			} catch (ex) {
				console.error('Failed to handle socket response', ex);
			}
		}
	}

	private handleError(ev: Event) {
		console.error('A socket error occurred', ev);
		errorHook.notify('WebSocket connection error');
	}
}

export const apiSocket = new ApiSocket();
