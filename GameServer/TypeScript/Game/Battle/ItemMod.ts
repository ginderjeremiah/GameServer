class ItemMod implements IInventoryItemMod, IItemMod {
    itemModId: number;
    itemSlotId: number;
    itemModName: string;
    itemModDesc: string;
    removable: boolean;
    slotTypeId: number;

    constructor(mod: IInventoryItemMod, modData: IItemMod) {
        this.itemModId = mod.itemModId;
        this.itemSlotId = mod.itemSlotId;
        this.itemModName = modData.itemModName;
        this.itemModDesc = modData.itemModDesc;
        this.removable = modData.removable;
        this.slotTypeId = modData.slotTypeId;
    }
}