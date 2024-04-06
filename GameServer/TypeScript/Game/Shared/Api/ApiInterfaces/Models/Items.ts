interface IItem {
	itemId: number;
	itemName: string;
	itemDesc: string;
	itemCategoryId: number;
}

interface IItemDrop {
	droppedById: number;
	itemId: number;
	dropRate: number;
}