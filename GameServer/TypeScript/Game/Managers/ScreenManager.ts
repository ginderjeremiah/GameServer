class ScreenManager {

    static #screens = document.getElementsByClassName("screen"); //reference to all screen elements

    static init() {
        document.getElementById("Fight")!.style.display = "inline-block"; //display Fight screen on startup
    }

    static changeScreen(newScreen: string) {
        LogManager.logMessage("Changing screen to: " + newScreen, "Debug");
        for (let i = 0; i < ScreenManager.#screens.length; i++) {
            let element = ScreenManager.#screens.item(i) as HTMLElement;
            element.style.display = element.id === newScreen ? "inline-block" : "none";
        }
    }

}
