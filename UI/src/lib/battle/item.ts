import { IItem, IInventoryItem } from '$lib/api';
import { ItemMod, newItemMod } from './item-mod';
import { BattleAttributes } from './battle-attributes';
import { staticData } from '$stores';

export interface Item extends IItem {
	itemId: number;
	equipped: boolean;
	equipmentSlotId?: number;
	favorite: boolean;
	appliedMods: ItemMod[];
	totalAttributes: BattleAttributes;
}

export const newItem = (invItem: IInventoryItem): Item => {
	const itemData = (staticData.items ?? [])[invItem.itemId];
	const appliedMods = invItem.appliedMods.map((am) => newItemMod(am));
	const allAttributes = [...itemData.attributes, ...appliedMods.flatMap((mod) => mod.attributes)];
	const totalAttributes = new BattleAttributes(allAttributes, false);
	return {
		...itemData,
		itemId: invItem.itemId,
		equipped: invItem.equipped,
		equipmentSlotId: invItem.equipmentSlotId,
		favorite: invItem.favorite ?? false,
		appliedMods,
		totalAttributes
	} satisfies Item;
};
