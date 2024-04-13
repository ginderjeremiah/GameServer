class InventoryManager {

    #maxInventoryItems: number = 23; //23 inventory slots and one trash slot
    inventory!: (Item | null)[]; //array containing inventory data
    inventorySlots: HTMLDivElement[] = [];
    #inventoryDisplay = document.getElementById("inventoryContainer") as HTMLDivElement; //reference to inventory HTML container
    equipped!: (Item | null)[]; //array Object containing equipped item data
    equippedIds: string[] = ["helmSlot", "chestSlot", "legSlot", "bootSlot", "weaponSlot", "accessorySlot"];
    equippedSlots: HTMLDivElement[] = [];
    equippedDisplay = document.getElementById("equipSlotsContainer") as HTMLDivElement;
    #equippedStats!: IBattlerAttribute[]; 
    #equippedStatsList = document.getElementById("equipStatsList") as HTMLUListElement; //reference to equipment HTML list for total equipped stats
    #delayedAction: DelayedAction;

    constructor(invData: IInventoryData) {
        const itemsData = DataManager.items;
        const itemModsData = DataManager.itemMods;
        this.inventory = invData.inventory.map((i) => {
            return i ? new Item(i, itemsData[i.itemId], itemModsData) : i
        });
        this.equipped = invData.equipped.map((i) => {
            return i ? new Item(i, itemsData[i.itemId], itemModsData) : i
        });
        this.#equippedStats = this.getEquippedStats();
        this.#initializeInventorySlots();
        this.#initializeEquipmentSlots();
        this.updateEquipmentStats();
        this.#delayedAction = new DelayedAction(5000, this.updateInventorySlots.bind(this));
    }

    #initializeEquipmentSlots(): void {
        let del = this.deleteItem.bind(this);
        let swap = this.swap.bind(this);
        let eq = this.equipped;
        this.equippedIds.forEach((id, i) => {
            const index = i;
            let equipSlot = document.createElement('div');
            let dragged = false;
            equipSlot.id = id;
            equipSlot.className = "equipBox";
            equipSlot.addEventListener('drop', (event: DragEvent) => {
                event.preventDefault();
                let type = event.dataTransfer?.getData("text/slotType");
                if (event.dataTransfer?.getData("text/invItem") === "true" && type) {
                    swap(type as SlotVariant, parseInt(event.dataTransfer.getData("text/plain")), "equipped", i);
                    equipSlot.classList.remove("highlight");
                    TooltipManager.setData(eq[index]!);
                    TooltipManager.updatePosition(event.clientX, event.clientY);
                    TooltipManager.setLock();
                }
            });
            equipSlot.addEventListener('dragover', function (event) {
                event.preventDefault();
                if (!dragged) {
                    equipSlot.classList.add("highlight");
                }
            });
            equipSlot.addEventListener('dragleave', function (event) {
                equipSlot.classList.remove("highlight");
            });
            equipSlot.addEventListener('mouseenter', (event: MouseEvent) => {
                if (eq[index]) {
                    TooltipManager.setData(eq[index]!);
                }
            });
            equipSlot.addEventListener('mousemove', (event: MouseEvent) => {
                if (eq[index]) {
                    TooltipManager.updatePosition(event.clientX, event.clientY);
                }
            });
            equipSlot.addEventListener('mouseleave', (event) => {
                TooltipManager.remove();
            });
            equipSlot.addEventListener('dragstart', (event) => {
                if (eq[index]) {
                    TooltipManager.remove();
                    if (event.ctrlKey === true) {
                        del("equipped", i);
                        event.preventDefault();
                    } else {
                        event.dataTransfer?.setData("text/plain", i.toString());
                        event.dataTransfer?.setData("text/slotType", "equipped");
                        event.dataTransfer?.setData("text/invItem", "true");
                        equipSlot.classList.add("darken");
                        dragged = true;
                    }
                } else {
                    event.preventDefault();
                }
            });
            equipSlot.addEventListener('dragend', (event) => {
                equipSlot.classList.remove("darken");
                dragged = false;
            });
            equipSlot.addEventListener('click', (event) => {
                if (event.ctrlKey === true) {
                    del("equipped", i);
                    TooltipManager.remove();
                }
            });
            equipSlot.draggable = true;
            this.equippedDisplay.appendChild(equipSlot);
            this.equippedSlots.push(equipSlot);
            const e = this.equipped[i];
            if (e) {
                this.#createItem(equipSlot, e.itemName);
            }
        });
    }

    #initializeInventorySlots(): void {
        let del = this.deleteItem.bind(this);
        let swap = this.swap.bind(this)
        let inv = this.inventory;
        for (let index = 0; index < this.#maxInventoryItems; index++) {
            const i = index;
            let invSlot = document.createElement('div');
            let dragged = false;
            invSlot.id = "iBox" + index;
            invSlot.className = "inventoryBox";
            invSlot.addEventListener('drop', (event: DragEvent) => {
                event.preventDefault();
                let type = event.dataTransfer?.getData("text/slotType");
                if (event.dataTransfer?.getData("text/invItem") === "true" && type) {
                    swap(type as SlotVariant, parseInt(event.dataTransfer.getData("text/plain")), "inventory", index);
                    invSlot.classList.remove("highlight");
                    TooltipManager.setData(inv[i]!);
                    TooltipManager.updatePosition(event.clientX, event.clientY);
                    TooltipManager.setLock();
                }
            });
            invSlot.addEventListener('dragover', function (event) {
                event.preventDefault();
                if (!dragged) {
                    invSlot.classList.add("highlight");
                }
            });
            invSlot.addEventListener('dragleave', function (event) {
                invSlot.classList.remove("highlight");
            });
            invSlot.addEventListener('mouseenter', (event) => {
                if (inv[i]) {
                    TooltipManager.setData(inv[i]!);
                }
            });
            invSlot.addEventListener('mousemove', (event) => {
                if (inv[i]) {
                    TooltipManager.updatePosition(event.clientX, event.clientY);
                }
            });
            invSlot.addEventListener('mouseleave', (event) => {
                TooltipManager.remove();
            });
            invSlot.addEventListener('dragstart', (event) => {
                if (inv[i]) {
                    TooltipManager.remove();
                    if (event.ctrlKey === true) {
                        del("inventory", index);
                        event.preventDefault();
                    } else {
                        event.dataTransfer?.setData("text/plain", index.toString());
                        event.dataTransfer?.setData("text/slotType", "inventory");
                        event.dataTransfer?.setData("text/invItem", "true");
                        dragged = true;
                        invSlot.classList.add("darken");
                    }
                } else {
                    event.preventDefault();
                }
            });
            invSlot.addEventListener('dragend', (event) => {
                invSlot.classList.remove("darken");
                dragged = false;
            });
            invSlot.addEventListener('click', (event) => {
                if (event.ctrlKey === true) {
                    del("inventory", index);
                    TooltipManager.remove();
                }
            });
            invSlot.draggable = true;
            this.#inventoryDisplay.appendChild(invSlot);
            this.inventorySlots.push(invSlot);
            const item = this.inventory[index];
            if (item) {
                this.#createItem(invSlot, item.itemName);
            }
        }
        let trashSlot = document.createElement('div');
        trashSlot.id = "iBox" + this.#maxInventoryItems;
        trashSlot.className = "inventoryBox";
        trashSlot.addEventListener('drop', function (event) {
            event.preventDefault();
            let type = event.dataTransfer?.getData("text/slotType");
            if (event.dataTransfer?.getData("text/invItem") === "true" && type) {
                del(type as SlotVariant, parseInt(event.dataTransfer.getData("text/plain")));
            }
        })
        trashSlot.addEventListener('dragover', function (event) {
            event.preventDefault();
        });
        /*trashSlot.addEventListener('mouseenter', (event) => {
            TooltipManager.setData(GameManager.getTrashData());
        });*/
        trashSlot.addEventListener('mousemove', (event) => {
            TooltipManager.updatePosition(event.clientX, event.clientY);
        });
        trashSlot.addEventListener('mouseleave', (event) => {
            TooltipManager.remove();
        });
        trashSlot.addEventListener('ondragstart', function (event) {
            return false;
        });

        this.#inventoryDisplay.appendChild(trashSlot);
        let trashIcon = document.createElement('img');
        trashIcon.className = "item";
        trashIcon.src = "img/Trash Can.png";
        trashIcon.draggable = false;
        trashSlot.appendChild(trashIcon);
    }

    //create an HTML element for item given its id and adds it to given slot
    #createItem(invSlot: HTMLDivElement, item: string) {
        let invItem = document.createElement('img');
        invItem.draggable = false;
        invItem.className = "item";
        invItem.src = "img/" + item + ".png";
        invSlot.appendChild(invItem);
    }

    //move item in slot 1 to slot 2. Swap if necessary. Returns true if equip recalculation needs to occur.
    swap(slotType1: SlotVariant, slot1: number, slotType2: SlotVariant, slot2: number): void {
        const base1 = this.getSlotContainers(slotType1);
        const base2 = this.getSlotContainers(slotType2);
        const box1 = base1.slots[slot1];
        const box2 = base2.slots[slot2];
        const source = base1.items[slot1];
        const target = base2.items[slot2];

        if (source) {
            base2.items[slot2] = source
            if (target) {
                if (box2.firstChild) {
                    box1.appendChild(box2.firstChild);
                }
                base1.items[slot1] = target;
                target.slotId = slot1;
            } else {
                delete base1.items[slot1];
            }
            if (box1.firstChild) {
                box2.appendChild(box1.firstChild);
            }
            source.slotId = slot2;
            if (slotType1 !== slotType2) {
                this.updateEquipmentStats();
                this.updateInventorySlots();
                GameManager.updateEquipmentStats();
            } else {
                this.startSave()
            }
        }
    }

    getSlotContainers(slotType: SlotVariant): { items: (Item | null)[], slots: HTMLDivElement[] } {
        switch (slotType) {
            case "equipped":
                return { items: this.equipped, slots: this.equippedSlots };
            case "inventory":
                return { items: this.inventory, slots: this.inventorySlots };
        }
    }

    //add items to first available inventory slots
    addItems(items: IInventoryItem[]): void {
        const itemsData = DataManager.items;
        const itemModsData = DataManager.itemMods;
        items.forEach(invItem => {
            const item = new Item(invItem, itemsData[invItem.itemId], itemModsData);
            if (this.inventory[invItem.slotId]) {
                const slotId = this.nextAvailableSlot();
                this.inventory[slotId] = item;
                item.slotId = slotId;
                this.startSave();
            } else {
                this.inventory[invItem.slotId] = item;
            }
            LogManager.logMessage("You found a " + item.itemName + "!", "Inventory");
            this.#createItem(this.inventorySlots[invItem.slotId], item.itemName);
        });
    }

    deleteItem(type: SlotVariant, slot: number): void {
        let base = this.getSlotContainers(type);
        base.items[slot] = null;
        base.slots[slot]?.firstChild?.remove();
        if (type == "equipped") {
            this.updateEquipmentStats();
            this.updateInventorySlots();
            GameManager.updateEquipmentStats();
        } else {
            this.startSave();
        }
    }

    getItemInfo(type: SlotVariant, slot: number): Item | null {
        return this.getSlotContainers(type).items[slot];
    }

    updateEquipmentStats() {
        this.#equippedStats = this.getEquippedStats();
        const listItems = new BattleAttributes(this.#equippedStats, false).getAttributeMap()
        .map((att) => {
            let listItem = document.createElement("li");
            listItem.textContent = att.name + " +" + att.value;
            return listItem;
        });
        this.#equippedStatsList.replaceChildren(...listItems);
        return this.#equippedStats;
    }

    async startSave() {
        this.#delayedAction.start();
    }

    reset(): void {
        this.#inventoryDisplay.replaceChildren();
        ["helmSlot", "chestSlot", "legSlot", "bootSlot", "weaponSlot", "accessorySlot"].forEach((slot) => {
            let invSlot = document.getElementById(slot) as HTMLDivElement;
            invSlot.replaceChildren();
        });
    }

    nextAvailableSlot() {
        for (let i = 0; i < this.inventory.length; i++) {
            if (!this.inventory[i]) {
                return i;
            }
        }
        return this.inventory.length - 1;
    }

    updateInventorySlots() {
        const inv = [
            ...this.inventory.flatMap((item) => item ? {inventoryItemId: item.inventoryItemId, slotId: item.slotId, equipped: false} : []),
            ...this.equipped.flatMap((item) => item ? {inventoryItemId: item.inventoryItemId, slotId: item.slotId, equipped: true} : [])
        ];
        DataManager.updateInventorySlots(inv);
    }

    getEquippedStats() {
        return [
            ...this.equipped.flatMap(item => item?.attributes ?? []),
            ...this.equipped.flatMap(item => item?.itemMods.flatMap(mod => mod.attributes) ?? [])
        ];
    }
}
