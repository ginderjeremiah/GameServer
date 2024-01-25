class DataCache<T> {
    #data?: T;
    #retrievalTask: Promise<T> | null;
    #retriever: () => Promise<T>

    constructor(retriever: () => Promise<T>) {
        this.#retriever = retriever;
        this.#retrievalTask = null;
    }

    get data() {
        if (this.#data) {
            return Promise.resolve(this.#data);
        } else if (!this.#retrievalTask) {
            this.#retrievalTask = this.#retriever().then(res => {
                this.#data = res;
                this.#retrievalTask = null;
                return res;
            });
        }
        return this.#retrievalTask
    }
}