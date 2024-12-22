import { ApiResponseType } from './types/api-type-map';

interface ApiResponseJson<T> {
	data: T;
	errorMessage?: string;
}

export class ApiResponse<T extends ApiResponseType> {
	#r: XMLHttpRequest;
	#responseJson?: ApiResponseJson<T>;

	constructor(r: XMLHttpRequest) {
		this.#r = r;
	}

	public get status() {
		return this.#r.status;
	}

	public get data(): T {
		if (!this.responseJson.data && this.responseJson.errorMessage) {
			throw new Error(this.error);
		} else {
			return this.responseJson.data;
		}
	}

	public get error() {
		return (
			this.responseJson?.errorMessage || this.#r.statusText || 'Failed to communicate with server.'
		);
	}

	public get responseText() {
		return this.#r.responseText;
	}

	private get responseJson() {
		return (this.#responseJson ??= this.parseJson());
	}

	private parseJson() {
		if (this.#r.responseText) {
			return JSON.parse(this.#r.responseText) as ApiResponseJson<T>;
		} else {
			return { data: undefined } as ApiResponseJson<T>;
		}
	}
}
