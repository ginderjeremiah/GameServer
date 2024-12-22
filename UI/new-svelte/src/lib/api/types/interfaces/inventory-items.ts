export interface IInventoryItemMod {
	itemModId: number;
	itemModSlotId: number;
};

export interface IInventoryItem {
	id: number;
	itemId: number;
	rating: number;
	equipped: boolean;
	inventorySlotNumber: number;
	itemMods: IInventoryItemMod[];
};

export interface IInventoryData {
	inventory: (IInventoryItem | undefined)[];
	equipped: (IInventoryItem | undefined)[];
};

export interface IInventoryUpdate {
	id: number;
	inventorySlotNumber: number;
	equipped: boolean;
};