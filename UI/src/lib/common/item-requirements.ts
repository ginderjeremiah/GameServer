import type { IItem } from '$lib/api';

/* Item equip-gate helpers. An item may be gated behind a proficiency level: an ungated item
   (`requiredProficiencyId == null`) is always equippable; a gated item can only be equipped once the
   player has reached `requiredProficiencyLevel` in that proficiency. The backend enforces the same rule
   at equip time as anti-cheat (see `Item.MeetsProficiencyRequirement`); this is the UI-side gate. */

/** A resolved view of an item's proficiency gate against the player's current level in that proficiency. */
export interface ItemProficiencyRequirement {
	proficiencyId: number;
	requiredLevel: number;
	currentLevel: number;
	met: boolean;
}

/**
 * The item's proficiency gate resolved against the player's current level (via `levelOf`), or undefined
 * when the item is ungated.
 */
export function itemProficiencyRequirement(
	item: IItem,
	levelOf: (proficiencyId: number) => number
): ItemProficiencyRequirement | undefined {
	if (item.requiredProficiencyId == null) {
		return undefined;
	}
	const currentLevel = levelOf(item.requiredProficiencyId);
	return {
		proficiencyId: item.requiredProficiencyId,
		requiredLevel: item.requiredProficiencyLevel,
		currentLevel,
		met: currentLevel >= item.requiredProficiencyLevel
	};
}

/** Whether the player meets the item's proficiency gate (always true for an ungated item). */
export function meetsItemProficiencyRequirement(item: IItem, levelOf: (proficiencyId: number) => number): boolean {
	const requirement = itemProficiencyRequirement(item, levelOf);
	return requirement == null || requirement.met;
}
