import {
	ApiEndpoint,
	ApiEndpointWithRequest,
	ApiEndpointNoRequest,
	ApiRequestTypes,
	ApiResponseTypes
} from './types/api-type-map';
import { ApiResponse } from './api-response';
import { keys } from '../common/functions';

export class ApiRequest<U extends ApiEndpoint> {
	r: XMLHttpRequest;
	endpoint: U;

	constructor(endpoint: U) {
		this.r = new XMLHttpRequest();
		this.endpoint = endpoint;
	}

	public get(): U extends ApiEndpointNoRequest ? Promise<ApiResponse<ApiResponseTypes[U]>> : never;
	public get(
		urlParams: U extends ApiEndpointWithRequest ? ApiRequestTypes[U] : never
	): Promise<ApiResponse<ApiResponseTypes[U]>>;
	// eslint-disable-next-line @typescript-eslint/no-explicit-any -- implementation signature behind the typed overloads above; the per-endpoint request DTOs aren't assignable to a concrete index signature.
	public get(urlParams?: Record<string, any>) {
		const params = this.encodeParams(urlParams);
		const endpoint = params ? this.endpoint + '?' + params : this.endpoint;
		const finalEndpoint = `/api/${endpoint}`;
		const r = this.r;
		r.withCredentials = true;
		r.open('GET', finalEndpoint, true);
		return new Promise<ApiResponse<ApiResponseTypes[U]>>((resolved) => {
			r.onload = () => resolved(new ApiResponse(r));
			r.onerror = r.onload;
			r.onabort = r.onerror;
			try {
				r.send();
			} catch {
				resolved(new ApiResponse(r));
			}
		});
	}

	public static get<U extends ApiEndpointNoRequest>(endpoint: U): Promise<ApiResponseTypes[U]>;
	public static get<U extends ApiEndpointWithRequest>(
		endpoint: U,
		urlParams: ApiRequestTypes[U]
	): Promise<ApiResponseTypes[U]>;
	// eslint-disable-next-line @typescript-eslint/no-explicit-any -- implementation signature behind the typed overloads above; the per-endpoint request DTOs can't be expressed as a single concrete param type.
	public static async get<U extends ApiEndpoint>(endpoint: U, urlParams?: any) {
		const request = new ApiRequest(endpoint);
		const result = await request.get(urlParams);
		return result.data;
	}

	public post(): U extends ApiEndpointNoRequest ? Promise<ApiResponse<ApiResponseTypes[U]>> : never;
	public post<T extends U & ApiEndpointWithRequest>(
		payload: ApiRequestTypes[T]
	): Promise<ApiResponse<ApiResponseTypes[U]>>;
	// eslint-disable-next-line @typescript-eslint/no-explicit-any -- implementation signature behind the typed overloads above; the per-endpoint request DTOs aren't assignable to a concrete index signature.
	public post(payload?: any) {
		const r = this.r;
		const endpoint = `/api/${this.endpoint}`;
		r.open('POST', endpoint, true);
		r.setRequestHeader('content-type', 'application/json');
		r.withCredentials = true;
		return new Promise<ApiResponse<ApiResponseTypes[U]>>((resolved) => {
			r.onload = () => resolved(new ApiResponse(r));
			r.onerror = r.onload;
			r.onabort = r.onerror;
			try {
				r.send(payload === undefined ? undefined : JSON.stringify(payload));
			} catch {
				resolved(new ApiResponse(r));
			}
		});
	}

	public static post<U extends ApiEndpointNoRequest>(endpoint: U): Promise<ApiResponseTypes[U]>;
	public static post<U extends ApiEndpointWithRequest>(
		endpoint: U,
		payload: ApiRequestTypes[U]
	): Promise<ApiResponseTypes[U]>;
	// eslint-disable-next-line @typescript-eslint/no-explicit-any -- implementation signature behind the typed overloads above; the per-endpoint request DTOs can't be expressed as a single concrete param type.
	public static async post<U extends ApiEndpoint>(endpoint: U, payload?: any) {
		const request = new ApiRequest(endpoint);
		const result = await request.post(payload);
		return result.data;
	}

	private encodeParams(urlParams?: Record<string, string | number | boolean>) {
		if (!urlParams) {
			return '';
		}

		return keys(urlParams)
			.filter((key) => urlParams[key] !== undefined)
			.map((key) => key + '=' + window.encodeURIComponent(urlParams[key]))
			.join('&');
	}
}
