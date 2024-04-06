interface IChange<T0> {
	item: T0;
	changeType: ChangeType;
}

interface ISetTagsData {
	id: number;
	tagIds: number[];
}

interface ILoginCredentials {
	username: string;
	password: string;
}

interface IInventoryUpdate {
	inventoryItemId: number;
	slotId: number;
	equipped: boolean;
}