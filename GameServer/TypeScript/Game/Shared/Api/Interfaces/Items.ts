interface IAddEditItemAttributesData {
	itemId: number;
	changes: IChange<IBattlerAttribute>[];
}

interface IAddEditItemModAttributesData {
	itemModId: number;
	changes: IChange<IBattlerAttribute>[];
}

interface IItem {
	id: number;
	name: string;
	description: string;
	itemCategoryId: number;
	iconPath: string;
	attributes: IBattlerAttribute[];
}

interface IItemCategory {
	id: number;
	name: string;
}

interface IItemDrop {
	itemId: number;
	dropRate: number;
}

interface IItemMod {
	id: number;
	name: string;
	removable: boolean;
	description: string;
	slotTypeId: number;
	attributes: IBattlerAttribute[];
}

interface IItemSlot {
	id: number;
	itemId: number;
	slotTypeId: number;
	guaranteedItemModId?: number;
	probability: number;
}

interface ISlotType {
	id: number;
	name: string;
}