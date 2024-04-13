interface IInventoryData {
	inventory: IInventoryItem[];
	equipped: IInventoryItem[];
}

interface IInventoryItem {
	inventoryItemId: number;
	itemId: number;
	rating: number;
	equipped: boolean;
	inventorySlotNumber: number;
	itemMods: IInventoryItemMod[];
}

interface IInventoryItemMod {
	itemModId: number;
	itemSlotId: number;
}

interface IInventoryUpdate {
	inventoryItemId: number;
	inventorySlotNumber: number;
	equipped: boolean;
}