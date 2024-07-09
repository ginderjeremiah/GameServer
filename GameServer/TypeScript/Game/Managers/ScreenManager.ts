import { Screens } from "../Shared/CustomTypes";
import { LogManager } from "./LogManager";

export class ScreenManager {
    static #screens = document.getElementsByClassName("screen"); //reference to all screen elements
    static currentScreen: Screens = "Fight"

    static init() {
        document.getElementById(this.currentScreen)!.style.display = "inline-block"; //display Fight screen on startup
    }

    static changeScreen(newScreen: Screens) {
        LogManager.logMessage("Changing screen to: " + newScreen, "Debug");
        this.currentScreen = newScreen;
        for (let i = 0; i < ScreenManager.#screens.length; i++) {
            let element = ScreenManager.#screens.item(i) as HTMLElement;
            element.style.display = element.id === newScreen ? "inline-block" : "none";
        }
    }

}
