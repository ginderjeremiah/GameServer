class ApiRequest<U extends ApiEndpoint> {
    r: XMLHttpRequest;
    endpoint: U;

    constructor(endpoint: U) {
        this.r = new XMLHttpRequest();
        this.endpoint = endpoint;
    }

    public get<T extends ApiEndpointNoRequest>(urlParams?: never): Promise<ApiResponse<ApiResponseTypes[U]>>;
    public get<T extends ApiEndpointWithRequest>(urlParams: ApiRequestTypes[T]): Promise<ApiResponse<ApiResponseTypes[U]>>;
    public get(urlParams?: any) {
        const params = this.encodeParams(urlParams)
        const endpoint = params
            ? this.endpoint + '?' + params
            : this.endpoint;
        this.r.open('GET', endpoint, true);
        const r = this.r;
        return new Promise<ApiResponse<ApiResponseTypes[U]>>((resolved, rejected) => {
            r.onload = (ev) => resolved(new ApiResponse(r, ev));
            r.onerror = (ev) => rejected(new ApiResponse(r, ev));
            r.onabort = r.onerror;
            r.send();
        });
    }

    public static get<U extends ApiEndpointNoRequest>(endpoint: U, urlParams?: never): Promise<ApiResponseTypes[U]>
    public static get<U extends ApiEndpointWithRequest>(endpoint: U, urlParams?: ApiRequestTypes[U]): Promise<ApiResponseTypes[U]>
    public static async get<U extends ApiEndpoint>(endpoint: U, urlParams?: any) {
        const request = new ApiRequest(endpoint);
        const result = await request.get(urlParams);
        return result.data;
    }
    
    public post<T extends U & ApiEndpointWithRequest>(payload: ApiRequestTypes[T]) {
        this.r.open('POST', this.endpoint, true); 
        const r = this.r;
        r.setRequestHeader('content-type', 'application/json');
        const p = payload;
        return new Promise<ApiResponse<ApiResponseTypes[U]>>((resolved, rejected) => {
            r.onload = (ev) => resolved(new ApiResponse(r, ev));
            r.onerror = (ev) => rejected(new ApiResponse(r, ev));
            r.onabort = r.onerror;
            r.send(JSON.stringify(p));
        });
    }

    public static async post<T extends ApiEndpointWithRequest>(endpoint: T, payload: ApiRequestTypes[T]) {
        const request = new ApiRequest(endpoint);
        const result = await request.post(payload);
        return result.data;
    }

    private encodeParams(urlParams: any) {
        return keys(urlParams)
            .filter(key => urlParams[key] !== undefined)
            .map(key => key + '=' + window.encodeURIComponent(urlParams[key]))
            .join('&');
    }
}
