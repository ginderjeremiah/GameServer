import { IAppliedModModel, IItemMod } from '$lib/api';
import { staticData } from '$stores';

export interface ItemMod extends Omit<IAppliedModModel, 'itemModId'>, IItemMod {}

export const newItemMod = (appliedMod: IAppliedModModel) => {
	const itemModData = staticData.itemMods[appliedMod.itemModId];
	return {
		...itemModData,
		itemModSlotId: appliedMod.itemModSlotId
	} satisfies ItemMod;
};
