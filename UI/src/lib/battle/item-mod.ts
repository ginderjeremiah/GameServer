import { IAppliedModModel, IItemMod } from '$lib/api';
import { staticData } from '$stores';

export interface ItemMod extends Omit<IAppliedModModel, 'itemModId'>, IItemMod {}

export const newItemMod = (appliedMod: IAppliedModModel): ItemMod | undefined => {
	// Resolve the mod's static definition by id; a missing/retired id yields no record, so degrade
	// gracefully (mirroring resolveUnlockReward) rather than spreading `undefined` and crashing.
	const itemModData = staticData.itemMods?.[appliedMod.itemModId];
	if (!itemModData) {
		return undefined;
	}
	return {
		...itemModData,
		itemModSlotId: appliedMod.itemModSlotId
	} satisfies ItemMod;
};
