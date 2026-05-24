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