import { EAttribute, EModifierType } from '$lib/api';
import { EAttributeModifierSource, type AttributeModifier } from './attribute-modifier';

/** One entry of a class's locked-base attribute fingerprint: a level-scaled distribution of a single
 *  attribute. Mirrors the backend reference contract `AttributeDistribution` (the `{ attributeId, baseAmount,
 *  amountPerLevel }` shape carried on the `Class` reference data and delivered to the client by the class
 *  reference delivery, #1225) — the same shape an enemy's distribution uses. */
export interface ClassAttributeDistribution {
	readonly attributeId: EAttribute;
	readonly baseAmount: number;
	readonly amountPerLevel: number;
}

/** The battle attribute modifiers a character of the given class has at `level` from its locked base — the
 *  level-scaled, non-reallocatable attribute fingerprint. Each distribution contributes `baseAmount +
 *  amountPerLevel × level` additively, the exact mirror of the backend's
 *  `AttributeDistribution.GetDistributionModifier` (and the same math an enemy's distribution uses), so the
 *  locked base composes into a battler's attributes identically on both sides — a frontend↔backend parity
 *  surface the class system adds (spike #1126 area D). The produced modifiers are fed into the player battler
 *  at assembly — alongside the proficiency bonuses, before the static engine modifiers — once the class
 *  reference data and the player's class id reach the client (#1225). */
export function classLockedBaseModifiers(
	distributions: readonly ClassAttributeDistribution[],
	level: number
): AttributeModifier[] {
	return distributions.map((distribution) => ({
		attribute: distribution.attributeId,
		amount: distribution.baseAmount + distribution.amountPerLevel * level,
		type: EModifierType.Additive,
		source: EAttributeModifierSource.AttributeDistribution
	}));
}
