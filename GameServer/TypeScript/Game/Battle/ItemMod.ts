class ItemMod implements IItemMod {
    id: number;
    itemSlotId: number;
    name: string;
    description: string;
    removable: boolean;
    slotTypeId: number;
    attributes: IBattlerAttribute[];

    constructor(mod: IInventoryItemMod, modData: IItemMod) {
        this.id = mod.itemModId;
        this.itemSlotId = mod.itemSlotId;
        this.name = modData.name;
        this.description = modData.description;
        this.removable = modData.removable;
        this.slotTypeId = modData.slotTypeId;
        this.attributes = modData.attributes;
    }
}