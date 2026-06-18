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

	/**
	 * Whether the request succeeded: a 2xx status with no error message in the body (the backend signals
	 * business failures via an `errorMessage` on an otherwise-200 response). Use this for control flow —
	 * `error` is a display-message accessor that always returns a non-empty string (it falls back to the
	 * status text or a generic message), so it must never be used as a boolean success check.
	 */
	public get ok(): boolean {
		return this.status >= 200 && this.status < 300 && !this.responseJson.errorMessage;
	}

	/**
	 * The parsed response payload. Throws whenever the body carries an `errorMessage` — regardless of
	 * whether `data` is also populated — so the throw-on-error `ApiRequest.get`/`post` helpers can never
	 * treat a business failure as success. This keys off `errorMessage` alone, matching `ok` and the
	 * socket layer's `fetchSocketData`.
	 */
	public get data(): T {
		if (this.responseJson.errorMessage) {
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
