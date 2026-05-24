import { IAppliedMod, IItemMod } from '$lib/api';
import { staticData } from '$stores';

export interface ItemMod extends Omit<IAppliedMod, 'itemModId'>, IItemMod {}

export const newItemMod = (appliedMod: IAppliedMod) => {
	const itemModData = staticData.itemMods[appliedMod.itemModId];
	return {
		...itemModData,
		itemModSlotId: appliedMod.itemModSlotId
	} satisfies ItemMod;
};
