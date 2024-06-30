interface IInventoryData {
	inventory: IInventoryItem[];
	equipped: IInventoryItem[];
}

interface IInventoryItem {
	id: number;
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
	id: number;
	inventorySlotNumber: number;
	equipped: boolean;
}