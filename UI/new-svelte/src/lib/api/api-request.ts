import { ApiEndpoint, ApiEndpointWithRequest, ApiEndpointNoRequest, ApiRequestTypes, ApiResponseTypes } from "./api-type-map";
import { ApiResponse } from "./api-response";
import { keys } from "../common/functions";

export class ApiRequest<U extends ApiEndpoint> {
    r: XMLHttpRequest;
    endpoint: U;

    constructor(endpoint: U) {
        this.r = new XMLHttpRequest();
        this.endpoint = endpoint;
    }

    public get<U extends ApiEndpointNoRequest>(): Promise<ApiResponse<ApiResponseTypes[U]>>;
    public get<U extends ApiEndpointWithRequest>(urlParams: ApiRequestTypes[U]): Promise<ApiResponse<ApiResponseTypes[U]>>;
    public get(urlParams?: Record<string, any>) {
        const params = this.encodeParams(urlParams)
        const endpoint = params
            ? this.endpoint + '?' + params
            : this.endpoint;
        const finalEndpoint = `https://localhost:7054/api/${endpoint}`;
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

    public static get<U extends ApiEndpointNoRequest>(endpoint: U): Promise<ApiResponseTypes[U]>
    public static get<U extends ApiEndpointWithRequest>(endpoint: U, urlParams?: ApiRequestTypes[U]): Promise<ApiResponseTypes[U]>
    public static async get<U extends ApiEndpoint>(endpoint: U, urlParams?: any) {
        const request = new ApiRequest(endpoint);
        const result = await request.get(urlParams);
        return result.data;
    }

    public post<T extends U & ApiEndpointWithRequest>(payload: ApiRequestTypes[T]) {
        const r = this.r;
        const endpoint = `https://localhost:7054/api/${this.endpoint}`;
        r.open('POST', endpoint, true);
        r.setRequestHeader('content-type', 'application/json');
        r.withCredentials = true;
        const p = payload;
        return new Promise<ApiResponse<ApiResponseTypes[U]>>((resolved) => {
            r.onload = () => resolved(new ApiResponse(r));
            r.onerror = r.onload;
            r.onabort = r.onerror;
            try {
                r.send(JSON.stringify(p));
            } catch {
                resolved(new ApiResponse(r));
            }
        });
    }

    public static async post<T extends ApiEndpointWithRequest>(endpoint: T, payload: ApiRequestTypes[T]) {
        const request = new ApiRequest(endpoint);
        const result = await request.post(payload);
        return result.data;
    }

    private encodeParams(urlParams?: Record<string, any>) {
        return keys(urlParams)
            .filter(key => urlParams?.[key] !== undefined)
            .map(key => key + '=' + window.encodeURIComponent(urlParams?.[key]))
            .join('&');
    }
}
