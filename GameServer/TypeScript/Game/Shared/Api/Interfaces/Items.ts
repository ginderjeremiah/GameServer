interface IAddEditItemAttributesData {
	itemId: number;
	changes: IChange<IBattlerAttribute>[];
}

interface IAddEditItemModAttributesData {
	itemModId: number;
	changes: IChange<IBattlerAttribute>[];
}

interface IItem {
	itemId: number;
	itemName: string;
	itemDesc: string;
	itemCategoryId: number;
	iconPath: string;
	attributes: IBattlerAttribute[];
}

interface IItemCategory {
	itemCategoryId: number;
	categoryName: string;
}

interface IItemDrop {
	itemId: number;
	dropRate: number;
}

interface IItemMod {
	itemModId: number;
	itemModName: string;
	removable: boolean;
	itemModDesc: string;
	slotTypeId: number;
	attributes: IBattlerAttribute[];
}

interface IItemSlot {
	itemSlotId: number;
	itemId: number;
	slotTypeId: number;
	guaranteedId: number;
	probability: number;
}

interface ISlotType {
	slotTypeId: number;
	slotTypeName: string;
}