import { IInventoryItemMod, IItemMod } from "$lib/api";
import { staticData } from "$stores";

export interface ItemMod extends Omit<IInventoryItemMod, "itemModId">, IItemMod {

}

export const newItemMod = (itemMod: IInventoryItemMod) => {
    const itemModData = staticData.itemMods[itemMod.itemModId];
    return {
        ...itemModData,
        itemModSlotId: itemMod.itemModSlotId
    } satisfies ItemMod;
}