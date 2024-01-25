
class LogManager {

    static #logBox = document.getElementById("logBox") as HTMLDivElement;
    static #logPreferences: LogPreferences;
    static #updatedLogPreferences: LogPreferences;

    static async init() {
        this.#logPreferences = await DataManager.getLogPreferences()
        this.#updatedLogPreferences = Object.assign({}, this.#logPreferences);

        document.getElementById("loggingOptionsButton")!.addEventListener('click', async (event) => {
            await PopupManager.triggerPopup("loggingOptionsPopup");
            LogManager.saveSettingChanges();
        });

        let loggingSettingsList = document.getElementById("loggingOptionsList") as HTMLUListElement;

        Object.keys(LogManager.#logPreferences).forEach((setting) => {
            let row = document.createElement("tr");
            loggingSettingsList.firstElementChild!.appendChild(row);

            let label = document.createElement("td");
            row.appendChild(label);
            label.textContent = setting + ":";

            let dropdownCell = document.createElement("td");
            row.appendChild(dropdownCell);

            let dropdown = document.createElement("select");
            dropdown.className = "optionSelect";
            dropdown.addEventListener('change', (event) => {
                let target = event.target as HTMLSelectElement;
                LogManager.setSetting(setting, target.value == 'true')
            })
            dropdownCell.appendChild(dropdown);

            let enabled = document.createElement("option");
            enabled.label = "Enabled";
            enabled.value = "true";
            enabled.selected = LogManager.#logPreferences[setting];
            dropdown.appendChild(enabled);

            let disabled = document.createElement("option");
            disabled.label = "Disabled";
            disabled.value = "false";
            disabled.selected = !LogManager.#logPreferences[setting];
            dropdown.appendChild(disabled);
        });
    }

    static setSetting(id: string, value: boolean): void {
        this.#updatedLogPreferences[id] = value;
    }

    static saveSettingChanges() {
        const changes: Dict<any> = {};
        let changesExist = false;
        Object.keys(this.#logPreferences).forEach(pref => {
            if (this.#logPreferences[pref] !== this.#updatedLogPreferences[pref]) {
                changes[pref] = this.#updatedLogPreferences[pref];
                changesExist = true;
            }
        });
        if (changesExist) {
            Object.assign(this.#logPreferences, this.#updatedLogPreferences);
            DataManager.saveLogPreferences(changes);
        }
    }

    static logMessage(message: string, logType: string) {
        if (logType === "ERROR" || LogManager.#logPreferences[logType]) {
            let childElementCount = LogManager.#logBox.childElementCount;
            let newMessage = document.createElement('span');
            newMessage.className = "logMessage";
            newMessage.textContent = message;
            if (childElementCount >= 40) {
                LogManager.#logBox.lastChild!.remove();
            }
            LogManager.#logBox.prepend(newMessage);
        }
    }
}
