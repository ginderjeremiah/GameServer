import { ApiSocketCommand, ApiSocketCommandWithRequest, ApiSocketResponseTypes } from "./ApiSocketTypeMap";
import { IApiSocketResponse } from "./ApiSocket";

export class ApiSocketRequest<T extends ApiSocketCommand> {
    private dataPromise: Promise<IApiSocketResponse<T>>;
    private promiseResolver!: (value: IApiSocketResponse<T>) => void;
    id: string;
    commandName: T;
    parameters?: T extends ApiSocketCommandWithRequest ? ApiSocketResponseTypes[T] : undefined;

    constructor(id: string, commandName: T, parameters?: T extends ApiSocketCommandWithRequest ? ApiSocketResponseTypes[T] : undefined) {
        this.id = id;
        this.commandName = commandName;
        this.parameters = parameters;
        this.dataPromise = new Promise(resolve => this.promiseResolver = resolve);
    }

    public getCommandInfo() {
        const params = JSON.stringify(this.parameters);
        return {
            id: this.id,
            name: this.commandName,
            parameters: params
        };
    }

    public resolve(data: IApiSocketResponse<T>) {
        this.promiseResolver(data);
    }

    public async getResponse() {
        return await this.dataPromise;
    }
}