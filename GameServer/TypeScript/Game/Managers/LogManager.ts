
class LogManager {

    static #logBox = document.getElementById("logBox") as HTMLDivElement;
    static #logPreferences: ILogPreference[];
    static #updatedLogPreferences: ILogPreference[];

    static async init() {
        this.#logPreferences = await DataManager.getLogPreferences()
        this.#updatedLogPreferences = this.#logPreferences.map(pref => Object.assign({}, pref));

        document.getElementById("loggingOptionsButton")!.addEventListener('click', async (event) => {
            await PopupManager.triggerPopup("loggingOptionsPopup");
            LogManager.saveSettingChanges();
        });

        let loggingSettingsList = document.getElementById("loggingOptionsList") as HTMLUListElement;

        LogManager.#logPreferences.forEach((setting) => {
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
                LogManager.setSetting(setting.name, target.value == 'true')
            })
            dropdownCell.appendChild(dropdown);

            let enabled = document.createElement("option");
            enabled.label = "Enabled";
            enabled.value = "true";
            enabled.selected = setting.enabled;
            dropdown.appendChild(enabled);

            let disabled = document.createElement("option");
            disabled.label = "Disabled";
            disabled.value = "false";
            disabled.selected = !setting.enabled;
            dropdown.appendChild(disabled);
        });
    }

    static setSetting(id: string, value: boolean): void {
        const pref = this.#updatedLogPreferences.find(pref => pref.name === id);
        if (pref) {
            pref.enabled = value;
        }
    }

    static saveSettingChanges() {
        const changes: ILogPreference[] = [];
        let changesExist = false;
        this.#updatedLogPreferences.forEach(pref => {
            if (pref.enabled !== this.#logPreferences.find(updPref => updPref.name === pref.name)?.enabled) {
                changes.push(pref);
                changesExist = true;
            }
        });
        if (changesExist) {
            this.#logPreferences = this.#updatedLogPreferences.map(pref => Object.assign({}, pref));
            DataManager.saveLogPreferences(changes);
        }
    }

    static logMessage(message: string, logType: string) {
        if (logType === "ERROR" || LogManager.#logPreferences.find(pref => pref.name === logType)?.enabled) {
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
