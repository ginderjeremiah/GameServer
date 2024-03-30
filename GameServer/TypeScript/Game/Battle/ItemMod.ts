class ItemMod implements InventoryItemMod, ItemModData {
    itemModId: number;
    itemSlotId: number;
    itemModName: string;
    itemModDesc: string;
    removable: boolean;
    slotTypeId: number;

    constructor(mod: InventoryItemMod, modData: ItemModData) {
        this.itemModId = mod.itemModId;
        this.itemSlotId = mod.itemSlotId;
        this.itemModName = modData.itemModName;
        this.itemModDesc = modData.itemModDesc;
        this.removable = modData.removable;
        this.slotTypeId = modData.slotTypeId;
    }
}