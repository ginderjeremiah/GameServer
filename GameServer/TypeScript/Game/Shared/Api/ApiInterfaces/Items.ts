interface IItemMod {
	itemModId: number;
	itemModName: string;
	removable: boolean;
	itemModDesc: string;
	slotTypeId: number;
}

interface IItemSlot {
	itemSlotId: number;
	itemId: number;
	slotTypeId: number;
	guaranteedId: number;
	probability: number;
}

interface IItem {
	itemId: number;
	itemName: string;
	itemDesc: string;
	itemCategoryId: number;
}

interface IItemCategory {
	itemCategoryId: number;
	categoryName: string;
}

interface ISlotType {
	slotTypeId: number;
	slotTypeName: string;
}

interface IItemDrop {
	itemId: number;
	dropRate: number;
}