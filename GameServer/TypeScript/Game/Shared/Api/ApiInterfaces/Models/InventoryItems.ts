interface IInventoryItem {
	inventoryItemId: number;
	playerId: number;
	itemId: number;
	rating: number;
	equipped: boolean;
	slotId: number;
	itemMods: IInventoryItemMod[];
}

interface IInventoryItemMod {
	itemModId: number;
	itemSlotId: number;
}