import { ApiSocketCommand, ApiSocketCommandWithRequest, ApiSocketRequestTypes } from './types/api-socket-type-map';
import { IApiSocketResponse } from './api-socket';

export class ApiSocketRequest<T extends ApiSocketCommand | void = void> {
	private dataPromise: Promise<IApiSocketResponse<T>>;
	private promiseResolver: (value: IApiSocketResponse<T>) => void;
	id: string;
	commandName: T;
	parameters?: T extends ApiSocketCommandWithRequest ? ApiSocketRequestTypes[T] : never;

	constructor(
		id: string,
		commandName: T,
		parameters?: T extends ApiSocketCommandWithRequest ? ApiSocketRequestTypes[T] : never
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

	/** Settles a still-pending request with an error response that carries no data — e.g. when the socket
	 *  drops before the server answered. Mirrors the shape of a server-reported error so callers handle it
	 *  through the same `response.error` path they already use, rather than a separate reject channel. */
	public settleWithError(error: string) {
		// An error response carries no `data` (matching how the server signals errors), so the cast bridges
		// the type's non-optional `data` field that callers already guard with optional chaining.
		this.promiseResolver({ id: this.id, name: this.commandName, error } as IApiSocketResponse<T>);
	}

	public async getResponse() {
		return await this.dataPromise;
	}
}
