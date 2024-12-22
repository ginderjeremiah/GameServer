import {
	ApiSocketCommand,
	ApiSocketCommandWithRequest,
	ApiSocketResponseTypes
} from './types/api-socket-type-map';
import { IApiSocketResponse } from './api-socket';

export class ApiSocketRequest<T extends ApiSocketCommand | void = void> {
	private dataPromise: Promise<IApiSocketResponse<T>>;
	private promiseResolver: (value: IApiSocketResponse<T>) => void;
	id: string;
	commandName: T;
	parameters?: T extends ApiSocketCommandWithRequest ? ApiSocketResponseTypes[T] : never;

	constructor(
		id: string,
		commandName: T,
		parameters?: T extends ApiSocketCommandWithRequest ? ApiSocketResponseTypes[T] : never
	) {
		this.id = id;
		this.commandName = commandName;
		this.parameters = parameters;
		const { promise, resolve } = Promise.withResolvers<IApiSocketResponse<T>>();
		this.dataPromise = promise;
		this.promiseResolver = resolve;
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
