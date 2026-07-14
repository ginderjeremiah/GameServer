import { EItemModType, ERarity, IAppliedModModel, IBattlerAttribute, IItemMod } from '$lib/api';
import { staticData } from '$stores';

// A class (not a plain object literal) so `statify` recognizes it via its own per-field reactivity
// (see `statify.svelte.ts`) instead of falling through to Svelte's native deep-proxy, which would give
// the same underlying mod a different identity depending on which reactive array reads it (#1957).
export class ItemMod implements IItemMod {
	id: number;
	name: string;
	description: string;
	itemModTypeId: EItemModType;
	rarityId: ERarity;
	attributes: IBattlerAttribute[];
	tags: number[];
	designerNotes: string;
	retiredAt?: string;

	itemModSlotId: number;

	constructor(itemModData: IItemMod, itemModSlotId: number) {
		this.id = itemModData.id;
		this.name = itemModData.name;
		this.description = itemModData.description;
		this.itemModTypeId = itemModData.itemModTypeId;
		this.rarityId = itemModData.rarityId;
		this.attributes = itemModData.attributes;
		this.tags = itemModData.tags;
		this.designerNotes = itemModData.designerNotes;
		this.retiredAt = itemModData.retiredAt;
		this.itemModSlotId = itemModSlotId;
	}
}

export const newItemMod = (appliedMod: IAppliedModModel): ItemMod | undefined => {
	// Resolve the mod's static definition by id; a missing/retired id yields no record, so degrade
	// gracefully (mirroring resolveUnlockReward) rather than spreading `undefined` and crashing.
	const itemModData = staticData.itemMods?.[appliedMod.itemModId];
	if (!itemModData) {
		return undefined;
	}
	return new ItemMod(itemModData, appliedMod.itemModSlotId);
};
