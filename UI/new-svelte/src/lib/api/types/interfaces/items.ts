import type { EItemCategory, EItemModType, IBattlerAttribute } from "../"

export interface IItemMod {
	id: number;
	name: string;
	removable: boolean;
	description: string;
	itemModTypeId: number;
	attributes: IBattlerAttribute[];
};

export interface IItem {
	id: number;
	name: string;
	description: string;
	itemCategoryId: EItemCategory;
	iconPath: string;
	attributes: IBattlerAttribute[];
};

export interface IItemModSlot {
	id: number;
	itemId: number;
	itemModSlotTypeId: EItemModType;
};

export interface IItemCategory {
	id: number;
	name: string;
};

export interface IItemModType {
	id: number;
	name: string;
};