import { IItem, IInventoryItem } from '$lib/api';
import { ItemMod, newItemMod } from './item-mod';
import { BattleAttributes } from './battle-attributes';
import { staticData } from '$stores';

export interface Item extends Omit<IInventoryItem, 'itemMods'>, IItem {
	itemMods: ItemMod[];
	totalAttributes: BattleAttributes;
}

export const newItem = (invItem: IInventoryItem): Item => {
	const itemData = staticData.items[invItem.itemId];
	const itemMods = invItem.itemMods.map((invMod) => newItemMod(invMod));
	const allAttributes = [...itemData.attributes, ...itemMods.flatMap((mod) => mod.attributes)];
	const totalAttributes = new BattleAttributes(allAttributes, false);
	return {
		...itemData,
		...invItem,
		itemMods,
		totalAttributes
	} satisfies Item;
};

export const getTrashItem = (): Item => {
	const itemData = staticData.items[0];
	return {
		...itemData,
		itemMods: [],
		id: -1,
		itemId: 0,
		totalAttributes: new BattleAttributes(),
		rating: 0,
		equipped: false,
		inventorySlotNumber: -1
	} satisfies Item;
};
