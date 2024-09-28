import { ApiSocketCommand, ApiSocketResponseTypes, ApiSocketCommandNoRequest, ApiSocketCommandWithRequest, ApiSocketRequestTypes } from "./api-socket-type-map";
import { ApiSocketRequest } from "./api-socket-request";
import { Action } from "../common/types";

export interface IApiSocketResponse<T extends ApiSocketCommand | void = void> {
    id: string,
    name: T,
    error?: string,
    data: T extends ApiSocketCommand ? ApiSocketResponseTypes[T] : never;
}

let socket: WebSocket;

export class ApiSocket {
    private socketCommandQueue: ApiSocketRequest<any>[] = [];
    private inFlightCommands: ApiSocketRequest<any>[] = [];
    private commandCounter = 0;
    private listeners: Partial<{ [key in ApiSocketCommand]: Action<IApiSocketResponse<key>>[] }> = {};

    private ensureSocket() {
        if (!socket || socket.readyState === socket.CLOSED) {
            socket = new WebSocket("https://localhost:7054/EstablishSocket");
            socket.onopen = this.processCommandQueue.bind(this);
            socket.onmessage = this.receiveResponse.bind(this);
            socket.onerror = this.handleError.bind(this);
        }
    }

    public async sendSocketCommand<T extends ApiSocketCommandNoRequest>(commandName: T): Promise<IApiSocketResponse<T>>
    public async sendSocketCommand<T extends ApiSocketCommandWithRequest>(commandName: T, urlParams: ApiSocketRequestTypes[T]): Promise<IApiSocketResponse<T>>
    public async sendSocketCommand<T extends ApiSocketCommand>(commandName: T, params?: any) {
        const id = (this.commandCounter++).toString();
        const request = new ApiSocketRequest(id, commandName, params);
        this.socketCommandQueue.push(request);
        this.processCommandQueue();
        return await request.getResponse();
    }

    public listenCommand<T extends ApiSocketCommand>(commandName: T, action: Action<IApiSocketResponse<T>>) {
        const listeners = this.listeners[commandName];
        if (listeners) {
            listeners.push(action);
        } else {
            this.listeners[commandName] = [];
            this.listeners[commandName].push(action);
        }
    }

    private processCommandQueue() {
        this.ensureSocket();
        if (socket.readyState === socket.OPEN) {
            let request: ApiSocketRequest | undefined;
            while (request = this.socketCommandQueue.shift()) {
                this.inFlightCommands.push(request);
                socket.send(JSON.stringify(request.getCommandInfo()));
            }
        }
    }

    private receiveResponse(ev: MessageEvent) {
        if (ev.data === "ping") {
            socket.send("pong");
        } else {
            const data = JSON.parse(ev.data) as IApiSocketResponse<ApiSocketCommand>;
            const listeners = this.listeners[data.name] ?? [];
            for (const listener of listeners) {
                (listener as Action<IApiSocketResponse<typeof data.name>>)(data);
            }

            if (data.id) {
                const response = this.inFlightCommands.find(c => c.id === data.id);
                response?.resolve(data)
            }
        }
    }

    private handleError(ev: Event) {
        console.log(ev);
    }
}