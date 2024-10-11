import {  } from "../"

export interface IInventoryItemMod {
	itemModId: number;
	itemSlotId: number;
}

export interface IInventoryItem {
	id: number;
	itemId: number;
	rating: number;
	equipped: boolean;
	inventorySlotNumber: number;
	itemMods: IInventoryItemMod[];
}

export interface IInventoryData {
	inventory: IInventoryItem[];
	equipped: IInventoryItem[];
}

export interface IInventoryUpdate {
	id: number;
	inventorySlotNumber: number;
	equipped: boolean;
}