class ZoneManager {

    #orderedZones!: IZone[];
    #currentZone: number;
    #zoneNumDisplay: HTMLSpanElement;
    #zoneTitleDisplay: HTMLSpanElement;

    constructor(currentZoneId: number) {
        this.#zoneNumDisplay = document.getElementById("zoneNum") as HTMLSpanElement;
        this.#zoneTitleDisplay = document.getElementById("zoneTitle") as HTMLSpanElement;
        this.#currentZone = 0;
        const zones = DataManager.zones.slice();
        this.#orderedZones = zones.sort((one, two) => one.order - two.order);
        this.#currentZone = this.getOrderedIndex(currentZoneId);
        this.updateZoneDisplay();
        document.getElementById("zoneButtonLeft")?.addEventListener('click', (event) => {
            GameManager.changeZone(-1);
        })
        document.getElementById("zoneButtonRight")?.addEventListener('click', (event) => {
            GameManager.changeZone(1);
        })
    }

    changeZone(amount: number) {
        const newZone = this.#currentZone + amount;
        if (newZone >= 0 && newZone < this.#orderedZones.length) {
            this.#currentZone = newZone;
            this.updateZoneDisplay();
            return true;
        }
        return false;
    }

    updateZoneDisplay(): void {
        this.#zoneNumDisplay.textContent = "Zone " + this.#currentZone;
        this.#zoneTitleDisplay.textContent = this.#orderedZones[this.#currentZone].name;
    }

    getZoneInfo() {
        return this.#orderedZones[this.#currentZone];
    }

    getOrderedIndex(zoneId: number) {
        for (let i = 0; i < this.#orderedZones.length; i++) {
            if (this.#orderedZones[i].id === zoneId)
                return i;
        }
        return 0;
    }

    get currentZoneId() {
        return this.#orderedZones[this.#currentZone].id;
    }
}
