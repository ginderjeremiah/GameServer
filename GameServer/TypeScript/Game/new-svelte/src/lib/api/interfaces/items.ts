import { IBattlerAttribute, IChange } from "../"

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
	itemCategoryId: number;
	iconPath: string;
	attributes: IBattlerAttribute[];
}

export interface IItemSlot {
	id: number;
	itemId: number;
	slotTypeId: number;
	guaranteedItemModId?: number;
	probability: number;
}

export interface IItemCategory {
	id: number;
	name: string;
}

export interface ISlotType {
	id: number;
	name: string;
}

export interface IItemDrop {
	itemId: number;
	dropRate: number;
}