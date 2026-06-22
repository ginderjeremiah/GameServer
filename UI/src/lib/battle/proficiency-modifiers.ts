import { EAttribute, EModifierType } from '$lib/api';
import { EAttributeModifierSource, type AttributeModifier } from './attribute-modifier';

/** One authored proficiency payout: a single attribute bonus granted at a level. Mirrors the backend
 *  reference contract `ProficiencyLevelModifier` (the flat per-level modifier list carried on the
 *  `Proficiency` reference data) — the shape the proficiency reference delivery (#1119) feeds in. */
export interface ProficiencyLevelModifier {
	readonly level: number;
	readonly attributeId: EAttribute;
	readonly modifierTypeId: EModifierType;
	readonly amount: number;
}

/** The battle attribute modifiers a player at `currentLevel` has earned from one proficiency: the bonuses of
 *  every authored payout level at or below `currentLevel`, as `Proficiency`-sourced {@link AttributeModifier}s.
 *  Cumulative ("the sum of the increments for every level reached") and the exact mirror of the backend's
 *  `Proficiency.ModifiersForLevel`, so a proficiency bonus composes into a battler's attributes identically on
 *  both sides — the one frontend↔backend parity surface the proficiency system adds (spike #982 area E). The
 *  produced modifiers are fed into the player battler at assembly once the proficiency reference data and the
 *  player's proficiency levels reach the client (#1119). */
export function proficiencyModifiers(
	levelModifiers: readonly ProficiencyLevelModifier[],
	currentLevel: number
): AttributeModifier[] {
	return levelModifiers
		.filter((modifier) => modifier.level <= currentLevel)
		.map((modifier) => ({
			attribute: modifier.attributeId,
			amount: modifier.amount,
			type: modifier.modifierTypeId,
			source: EAttributeModifierSource.Proficiency
		}));
}
