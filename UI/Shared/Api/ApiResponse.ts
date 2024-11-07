import { ApiResponseType } from "./ApiTypeMap";

export class ApiResponse<T extends ApiResponseType> {
    #r: XMLHttpRequest;
    #ev: ProgressEvent<EventTarget>;
    #responseJson?: { data: T, error: string}

    constructor(r: XMLHttpRequest, ev: ProgressEvent<EventTarget>) {
        this.#r = r;
        this.#ev = ev
    }

    public get status() {
        return this.#r.status;
    }

    public get data(): T {
        if (!this.responseJson.data && this.responseJson.error) {
            throw new Error(this.responseJson.error);
        }
        return this.responseJson.data;
    }

    public get error() {
        return this.responseJson.error
    }

    public get responseText() {
        return this.#r.responseText;
    }

    private get responseJson() {
        return this.#responseJson ??= JSON.parse(this.#r.responseText)
    }
}