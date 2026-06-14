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

export const newItem = (invItem: IInventoryItem): Item | undefined => {
	// Resolve the item's static definition by id; a missing/retired id yields no record, so degrade
	// gracefully (mirroring resolveUnlockReward) rather than spreading `undefined` and crashing.
	const itemData = staticData.items?.[invItem.itemId];
	if (!itemData) {
		return undefined;
	}
	// Drop any applied mod whose own definition is missing/retired so one stale mod can't crash the item.
	const appliedMods = invItem.appliedMods.map((am) => newItemMod(am)).filter((mod): mod is ItemMod => mod != null);
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
