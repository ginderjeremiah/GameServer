interface IApiSocketResponse<T extends ApiSocketCommand> {
    id: string,
    name: T,
    error?: string,
    data: ApiSocketResponseTypes[T];
}

class ApiSocket {
    private socket: WebSocket;
    private socketCommandQueue: ApiSocketRequest<any>[] = [];
    private inFlightCommands: ApiSocketRequest<any>[] = [];
    private commandCounter = 0;
    private listeners: Partial<{ [key in ApiSocketCommand]: Action<IApiSocketResponse<key>>[] }> = {};

    constructor() {
        this.socket = new WebSocket("/EstablishSocket");
        this.socket.onopen = this.processCommandQueue.bind(this);
        this.socket.onmessage = this.receiveResponse.bind(this);
        this.socket.onerror = this.handleError.bind(this);
    }

    public async sendSocketCommand<T extends ApiSocketCommandNoRequest>(commandName: T): Promise<IApiSocketResponse<T>>
    public async sendSocketCommand<T extends ApiSocketCommandWithRequest>(commandName: T, urlParams?: ApiSocketRequestTypes[T]): Promise<IApiSocketResponse<T>>
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
        }
    }

    private processCommandQueue() {
        if (this.socket.readyState === this.socket.OPEN) {
            let request: ApiSocketRequest<any> | undefined;
            while(request = this.socketCommandQueue.shift()) {
                this.inFlightCommands.push(request);
                this.socket.send(JSON.stringify(request.getCommandInfo()));
            }
        }
    }

    private receiveResponse(ev: MessageEvent) {
        if (ev.data === "ping") {
            this.socket.send("pong");
        } else {
            const data = JSON.parse(ev.data) as IApiSocketResponse<any>;
            for (const listener of this.listeners[data.name as ApiSocketCommand] ?? []) {
                listener(data as any);
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