import {
	EItemCategory,
	EItemModType,
	IBattlerAttribute,
	IChange
} from "../"

export interface IAddEditItemAttributesData {
	itemId: number;
	changes: IChange<IBattlerAttribute>[];
}

export interface IAddEditItemModAttributesData {
	itemModId: number;
	changes: IChange<IBattlerAttribute>[];
}

export interface IItemMod {
	id: number;
	name: string;
	removable: boolean;
	description: string;
	itemModTypeId: number;
	attributes: IBattlerAttribute[];
}

export interface IItem {
	id: number;
	name: string;
	description: string;
	itemCategoryId: EItemCategory;
	iconPath: string;
	attributes: IBattlerAttribute[];
}

export interface IItemModSlot {
	id: number;
	itemId: number;
	itemModSlotTypeId: EItemModType;
	guaranteedItemModId?: number;
	probability: number;
}

export interface IItemDrop {
	itemId: number;
	dropRate: number;
}

export interface IItemCategory {
	id: number;
	name: string;
}

export interface IItemModType {
	id: number;
	name: string;
}