export class PopupManager {
    static #popupContainer = document.getElementById("popupContainer") as HTMLDivElement;
    static #popups = document.getElementsByClassName("popup");
    static currentResolve: ((value: boolean | PromiseLike<boolean>) => void) | null;

    static #init = (() => {
        document.getElementById("popupOverlay")?.addEventListener("click", (event) => {
            PopupManager.clearPopups(true);
        });
    })()

    static clearPopups(cancelled: boolean) {
        this.#popupContainer.style.display = "none";
        if (this.currentResolve) {
            this.currentResolve(cancelled);
        }
        this.currentResolve = null;
    }

    static triggerPopup(popup: string) {
        this.#popupContainer.style.display = "block";
        for (let i = 0; i < this.#popups.length; i++) {
            let element = this.#popups.item(i) as HTMLDivElement;
            if (element.id === popup) {
                element.style.display = "block";
            } else {
                element.style.display = "none";
            }
        }
        return new Promise<boolean>((resolve) => {
            this.currentResolve = resolve;
        })
    }
}
