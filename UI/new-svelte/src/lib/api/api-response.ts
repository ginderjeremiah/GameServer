import { ApiResponseType } from './types/api-type-map';

interface ApiResponseJson<T> {
	data: T;
	errorMessage?: string;
}

/**
 * The pieces of an HTTP response the API client needs, decoupled from the underlying transport.
 * The request layer reads the body to text before constructing the response so the getters below can
 * stay synchronous regardless of how the bytes were fetched.
 */
export interface RawApiResponse {
	status: number;
	statusText: string;
	responseText: string;
}

export class ApiResponse<T extends ApiResponseType> {
	readonly #raw: RawApiResponse;
	#responseJson?: ApiResponseJson<T>;

	constructor(raw: RawApiResponse) {
		this.#raw = raw;
	}

	public get status() {
		return this.#raw.status;
	}

	public get data(): T {
		if (!this.responseJson.data && this.responseJson.errorMessage) {
			throw new Error(this.error);
		} else {
			return this.responseJson.data;
		}
	}

	public get error() {
		return this.responseJson?.errorMessage || this.#raw.statusText || 'Failed to communicate with server.';
	}

	public get responseText() {
		return this.#raw.responseText;
	}

	private get responseJson() {
		return (this.#responseJson ??= this.parseJson());
	}

	private parseJson() {
		if (this.#raw.responseText) {
			try {
				return JSON.parse(this.#raw.responseText) as ApiResponseJson<T>;
			} catch {
				return { data: undefined, errorMessage: 'Invalid server response.' } as ApiResponseJson<T>;
			}
		} else {
			return { data: undefined } as ApiResponseJson<T>;
		}
	}
}
