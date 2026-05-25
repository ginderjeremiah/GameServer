export interface IAppliedModModel {
	itemModId: number;
	itemModSlotId: number;
};

export interface IInventoryItem {
	itemId: number;
	equipped: boolean;
	equipmentSlotId?: number;
	appliedMods: IAppliedModModel[];
};

export interface IInventoryData {
	unlockedItems: IInventoryItem[];
	unlockedMods: number[];
};

export interface IEquipRequest {
	itemId: number;
	equipmentSlotId: number;
};

export interface IApplyModRequest {
	itemId: number;
	itemModId: number;
	itemModSlotId: number;
};

export interface IRemoveModRequest {
	itemId: number;
	itemModSlotId: number;
};