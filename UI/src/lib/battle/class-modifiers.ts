import { EAttribute, EModifierType, type ISignaturePassive } from '$lib/api';
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
 *  surface the class system adds (spike #1126 area D). The arithmetic is done in IEEE-754 double here **and on
 *  the backend** (which casts each `decimal` operand to double before the add/multiply, rather than computing
 *  in decimal then casting), so a fractional `amountPerLevel`/`baseAmount` stays bit-exact across the boundary
 *  — the anti-cheat replay compares with no tolerance. The produced modifiers are fed into the player battler
 *  at assembly, alongside the proficiency bonuses and before the static engine modifiers. */
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

/** The single battle attribute modifier a character's class signature passive contributes — the durable
 *  combat-identity bonus (spike #1126 area E), the exact mirror of the backend's
 *  `ClassSignaturePassive.GetModifier`. A purely flat passive (`scalingAttributeId` null) contributes
 *  `amount`; an attribute-scaled one adds `scalingAmount × resolveScalingValue(scalingAttributeId)`, reading
 *  the scaling attribute's already-assembled value (the snapshot state a V1 passive sees, like a skill effect
 *  reading its caster), so it never depends on itself. The arithmetic is IEEE-754 double here **and on the
 *  backend** (which casts each authored `decimal` operand to double before the add/multiply), so a fractional
 *  `amount`/`scalingAmount` stays bit-exact across the boundary — the anti-cheat replay compares with no
 *  tolerance. The produced modifier is added to the player battler **last** (after the locked base, proficiency
 *  bonuses, and the static engine modifiers), matching where the backend appends it at assembly. */
export function classSignaturePassiveModifier(
	passive: ISignaturePassive,
	resolveScalingValue: (attribute: EAttribute) => number
): AttributeModifier {
	// `!= null` catches both the omitted (`undefined`) and JSON-`null` forms a flat passive's optional scaling
	// attribute can take, so only a genuinely-present scaling attribute pulls in the scaled term.
	let amount = passive.amount;
	if (passive.scalingAttributeId != null) {
		amount += passive.scalingAmount * resolveScalingValue(passive.scalingAttributeId);
	}

	return {
		attribute: passive.attributeId,
		amount,
		type: passive.modifierType,
		source: EAttributeModifierSource.Class
	};
}
