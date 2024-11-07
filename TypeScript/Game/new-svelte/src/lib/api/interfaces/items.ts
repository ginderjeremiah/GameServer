import {
	EItemCategory,
	EItemModSlotType,
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
	slotTypeId: number;
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
	itemModSlotTypeId: EItemModSlotType;
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

export interface IItemModSlotType {
	id: number;
	name: string;
}