import { ApiResponseType } from "./api-type-map";

export class ApiResponse<T extends ApiResponseType> {
    #r: XMLHttpRequest;
    #responseJson?: { data: T, error: string }

    constructor(r: XMLHttpRequest) {
        this.#r = r;
    }

    public get status() {
        return this.#r.status;
    }

    public get data(): T {
        if (this.responseJson?.data) {
            return this.responseJson.data;
        } else {
            throw new Error(this.error);
        }
    }

    public get error() {
        return this.responseJson?.error || this.#r.statusText || "Failed to communicate with server.";
    }

    public get responseText() {
        return this.#r.responseText;
    }

    private get responseJson() {
        return this.#responseJson ??= this.parseJson()
    }

    private parseJson() {
        if (this.#r.responseText) {
            return JSON.parse(this.#r.responseText) as { data: T, error: string };
        } else {
            return undefined;
        }
    }
}