class TooltipManager {

    static tooltipElement = document.getElementById("tooltip") as HTMLDivElement;
    static tooltipTitle = document.getElementById("tooltipTitle") as HTMLHeadingElement;
    static tooltipContent = document.getElementById("tooltipContent") as HTMLDivElement;
    static displayedDataId: number = -1;
    static currData: Tooltippable;
    static currPlayerStats?: { baseStats: BaseStats, derivedStats: DerivedStats };
    static active: boolean = false;
    static lockEnd: number = 0;
    static lockAmount: number = 10;
    static lockActive: boolean = false;
    static #tooltipIds: number = 0;

    static getId(): number {
        return this.#tooltipIds++;
    }

    //set lock to prevent erroneous mouse events from interfering with tooltip after a drag
    static setLock() {
        this.lockActive = true;
        this.lockEnd = Date.now() + this.lockAmount;
    }

    static checkLock() {
        return this.lockActive ? (this.lockActive = Date.now() < this.lockEnd) : false;
    }

    static setData(data: Tooltippable) {
        if (!this.checkLock()) {
            this.currData = data;
            this.active = true;
        }
    }

    static refresh() {
        if (this.active) {
            this.displayedDataId = this.currData.updateTooltipData(this.tooltipTitle, this.tooltipContent, this.displayedDataId);
        }
    }

    static updatePosition(mouseX: number, mouseY: number) {
        this.active = true;
        //subtract 17px for scrollbar buffer
        if (this.tooltipElement.offsetWidth + mouseX + 15 < window.innerWidth - 17) {
            this.tooltipElement.style.left = (mouseX + 15) + "px";
            this.tooltipElement.style.right = "";
        } else {
            this.tooltipElement.style.left = "";
            this.tooltipElement.style.right = (window.innerWidth - mouseX + 15) + "px";
        }
        if (this.tooltipElement.offsetHeight + mouseY + 15 < window.innerHeight) {
            this.tooltipElement.style.top = (mouseY + 15) + "px";
            this.tooltipElement.style.bottom = "";
        } else {
            this.tooltipElement.style.top = "";
            this.tooltipElement.style.bottom = (window.innerHeight - mouseY + 15) + "px";
        }
        this.tooltipElement.style.display = "block";
    }

    static remove() {
        if (!this.checkLock()) {
            this.active = false;
            this.tooltipElement.style.display = "none";
        }
    }
}
