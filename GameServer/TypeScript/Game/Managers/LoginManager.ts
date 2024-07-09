import { DataManager } from "./DataManager";
import { PopupManager } from "./PopupManager";

export class LoginManager {
    
    static #username = document.getElementById("Username") as HTMLInputElement;
    static #password = document.getElementById("Password") as HTMLInputElement;
    static #errorSpan = document.getElementById("loginFailedReason") as HTMLSpanElement;
    
    static async showLogin() {
        const response = await DataManager.loginStatus();
        if(response.status !== 200) {
            return new Promise<void>((res) => {
                this.#show(res);
            });
        }
        return;
    }

    static async attemptLogin() {
        const username = this.#username.value;
        const password = this.#password.value;
        if (!username) {
            this.#errorSpan.textContent = "ERROR: Invalid Username."
        } else if (!password) {
            this.#errorSpan.textContent = "ERROR: Invalid Password."
        } else {
            const req = DataManager.login(username, password);
            req.then((response) => {
                if(response.status === 200) {
                    PopupManager.clearPopups(false);
                } else {
                    this.#errorSpan.textContent = "ERROR: " + response.error; 
                }
            })
        }
    }

    static async #show(res: (value: void | PromiseLike<void>) => void): Promise<any> {
        return PopupManager.triggerPopup("loginPopup").then(() => res(), () => this.#show(res))
    }
}