class ZoneManager {

    //#zones: ZoneData[];
    #orderedZones!: ZoneData[];
    #currentZone: number;
    #zoneNumDisplay: HTMLSpanElement;
    #zoneTitleDisplay: HTMLSpanElement;
    //#delayedAction: DelayedAction;

    constructor(currentZoneId: number) {
        this.#currentZone = 0;
        DataManager.zones.then(zones => this.#init(zones, currentZoneId));
        document.getElementById("zoneButtonLeft")?.addEventListener('click', (event) => {
            GameManager.changeZone(-1);
        })
        document.getElementById("zoneButtonRight")?.addEventListener('click', (event) => {
            GameManager.changeZone(1);
        })
        this.#zoneNumDisplay = document.getElementById("zoneNum") as HTMLSpanElement;
        this.#zoneTitleDisplay = document.getElementById("zoneTitle") as HTMLSpanElement;
        // this.#delayedAction = new DelayedAction(5000, () => (() => {
        //     DataManager.setCurrentZone(this.currentZoneId)
        // }).bind(this))
    }

    #init(zones: ZoneData[], currentZoneId: number) {
        this.#orderedZones = zones.sort((one, two) => one.zoneOrder - two.zoneOrder);
        this.#currentZone = this.getOrderedIndex(currentZoneId);
        this.updateZoneDisplay();
    }

    changeZone(amount: number) {
        const newZone = this.#currentZone + amount;
        if (newZone >= 0 && newZone < this.#orderedZones.length) {
            this.#currentZone = newZone;
            this.updateZoneDisplay();
        }
        return this.currentZoneId;
    }

    updateZoneDisplay(): void {
        this.#zoneNumDisplay.textContent = "Zone " + this.#currentZone;
        this.#zoneTitleDisplay.textContent = this.#orderedZones[this.#currentZone].zoneName;
    }

    getZoneInfo() {
        return this.#orderedZones[this.#currentZone];
    }

    getOrderedIndex(zoneId: number) {
        for (let i = 0; i < this.#orderedZones.length; i++) {
            if (this.#orderedZones[i].zoneId === zoneId)
                return i;
        }
        return 0;
    }

    get currentZoneId() {
        return this.#orderedZones[this.#currentZone].zoneId;
    }
}
