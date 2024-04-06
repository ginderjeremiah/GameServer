class ApiRequest<U extends ApiEndpoint> {
    r: XMLHttpRequest;
    endpoint: U;

    constructor(endpoint: U) {
        this.r = new XMLHttpRequest();
        this.endpoint = endpoint;
    }

    public get(urlParams?: ApiRequestTypes[U]) {
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

    public static get<U extends ApiEndpoint>(endpoint: U, urlParams?: ApiRequestTypes[U]) {
        const request = new ApiRequest(endpoint);
        return new Promise<ApiResponseTypes[U]>(async resolved => resolved((await request.get(urlParams)).data));
    }

    public post(payload?: ApiRequestTypes[U]) {
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

    public static post<U extends ApiEndpoint>(endpoint: U, payload?: ApiRequestTypes[U]) {
        const request = new ApiRequest(endpoint);
        return new Promise<ApiResponseTypes[U]>(async resolved => resolved((await request.post(payload)).data));
    }

    private encodeParams(urlParams: any) {
        return keys(urlParams)
            .filter(key => urlParams[key] !== undefined)
            .map(key => key + '=' + window.encodeURIComponent(urlParams[key]))
            .join('&');
    }
}
