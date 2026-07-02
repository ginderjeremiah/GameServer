/* The pure scalar formulas of the battle arithmetic — one definition consumed by both the
   battle classes (`Skill.calculateDamage`, `Battler.takeDamage`, `Battler.cdMultiplier`) and
   the display surfaces (the skills page, the in-battle skill tooltip), so the numbers the UI
   shows cannot drift from the numbers the simulation fights with (#347). The battle classes
   sit under the cross-implementation parity suite, which therefore guards these formulas
   against backend drift too. */

import { EAttribute, EDamageType, type IAttributeMultiplier, type ISkill, type ISkillEffect } from '$lib/api';
import { TOUGHNESS_MITIGATION_CONSTANT } from '$lib/api/types/game-constants';
import type { BattleAttributes } from './battle-attributes';
import { amplificationAttributes, resistanceAttributes } from './damage-types';

/** One attribute's contribution to a skill's raw damage. */
export interface SkillContribution {
	attributeId: EAttribute;
	multiplier: number;
	value: number;
}

/** One damage multiplier's scaled value at the given attributes. */
const contributionValue = (mult: IAttributeMultiplier, attributes: BattleAttributes): number =>
	attributes.getValue(mult.attributeId) * mult.multiplier;

/** Raw damage of a skill at the given attributes: base plus each multiplier applied to the
 *  attribute it scales. Accumulate the multiplier bonus from zero and add baseDamage last, exactly
 *  mirroring the backend grouping (`BattleSkill.CalculateDamage`): floating-point addition is not
 *  associative, so this `base + (m1 + m2 + …)` order keeps the result bit-for-bit identical and the
 *  anti-cheat replay in parity for skills with two or more damage multipliers. */
export function calculateSkillDamage(skill: ISkill, attributes: BattleAttributes): number {
	let multiplierBonus = 0;
	for (const mult of skill.damageMultipliers) {
		multiplierBonus += contributionValue(mult, attributes);
	}
	return skill.baseDamage + multiplierBonus;
}

/** Per-attribute breakdown of a skill's scaling at the given attributes — the decomposition of
 *  {@link calculateSkillDamage}'s multiplier sum, kept beside it so the two cannot disagree. */
export function skillContributions(skill: ISkill, attributes: BattleAttributes): SkillContribution[] {
	return skill.damageMultipliers.map((mult) => ({
		attributeId: mult.attributeId,
		multiplier: mult.multiplier,
		value: contributionValue(mult, attributes)
	}));
}

/** Damage dealt after the defender's {@link EAttribute.Toughness} mitigation curve. The curve reduces a hit by
 *  `Toughness / (Toughness + C)` (C = {@link TOUGHNESS_MITIGATION_CONSTANT}): effective HP is linear in
 *  Toughness while the reduction asymptotes below 100% (no immunity), and the constant denominator means an
 *  investment retains its mitigation % across all of progression (#1487, revising #1330's level normalization).
 *  With no Toughness the factor is an exact 1.0 and the hit is unchanged. Block's flat reduction was removed
 *  (#1330), so the stack is purely multiplicative and never needs a floor — `rawDamage` is already positive here
 *  (the absorption branch in {@link mitigateDamage} handles the non-positive case). The curve is unclamped below
 *  0 — a debuff-driven negative Toughness amplifies the hit (#1483), with the pole at `Toughness = −C` left
 *  unguarded per #1478 (unreachable by authored content). Mirrors the backend `Battler.ComputeNetDamage` — the
 *  expression must match bit-for-bit for battle parity. */
export function toughnessMitigatedDamage(rawDamage: number, toughness: number): number {
	const toughnessReduction = toughness / (toughness + TOUGHNESS_MITIGATION_CONSTANT);
	return rawDamage * (1 - toughnessReduction);
}

/** Amplifies an outgoing `rawDamage` hit of `damageType` by the ATTACKER's amplification:
 *  `rawDamage × (1 + Σ applies(type).amplification)`, the additive sum folded in the fixed
 *  {@link amplificationAttributes} order so both simulators agree bit-for-bit. With no amplification authored
 *  the sum is 0, so the factor is an exact 1.0 and the hit is unchanged (the reduce-to-today identity, #1320).
 *  Mirrors the backend `Battler.AmplifyDamage`. */
export function amplifiedDamage(
	rawDamage: number,
	damageType: EDamageType,
	attackerAttributes: BattleAttributes
): number {
	let amplification = 0;
	for (const attribute of amplificationAttributes(damageType)) {
		amplification += attackerAttributes.getValue(attribute);
	}
	return rawDamage * (1 + amplification);
}

/** The defender's total resistance to a hit of `damageType` — the additive sum of the applicable
 *  resistance attributes, folded in the fixed {@link resistanceAttributes} order so it matches the value
 *  {@link mitigateDamage} applies bit-for-bit. With none authored the sum is an exact 0. Shared by the
 *  mitigation math and the combat-log/floater resist feedback, so the percentage the UI reports can never
 *  drift from the percentage the simulation applied. */
export function resistanceTotal(damageType: EDamageType, defenderAttributes: BattleAttributes): number {
	let resistance = 0;
	for (const attribute of resistanceAttributes(damageType)) {
		resistance += defenderAttributes.getValue(attribute);
	}
	return resistance;
}

/** The net damage an incoming `dealt` hit (already amplified and crit-multiplied) of `damageType` deals to a
 *  defender: percentage resistance first (`dealt × (1 − Σ applies(type).resistance)`, UNCLAMPED — a negative
 *  total amplifies as vulnerability, a total above 1 drives the result negative as absorption), then — only
 *  while the post-resistance damage is still positive — the {@link EAttribute.Toughness} mitigation curve,
 *  so mitigation can neither heal nor deepen an absorption heal. The resistance sum
 *  is folded in the fixed {@link resistanceAttributes} order for parity; with no resistance and no Toughness the
 *  positive branch reduces to `dealt`. A negative result heals the defender. With Block's flat reduction removed
 *  (#1330) the whole stack is multiplicative. Mirrors the backend `Battler.ComputeNetDamage`. */
export function mitigateDamage(dealt: number, damageType: EDamageType, defenderAttributes: BattleAttributes): number {
	const mitigated = dealt * (1 - resistanceTotal(damageType, defenderAttributes));
	if (mitigated <= 0) {
		// Absorption (or a zero hit): the defender takes a net heal and mitigation never applies.
		return mitigated;
	}
	return toughnessMitigatedDamage(mitigated, defenderAttributes.getValue(EAttribute.Toughness));
}

/** The cooldown multiplier — the CooldownRecovery attribute read directly. It is a base-1 multiplier
 *  (1.0 = normal charge speed, 1.09 = +9%), so a multiplicative buff scales it intuitively. */
export function cooldownMultiplier(attributes: BattleAttributes): number {
	return attributes.getValue(EAttribute.CooldownRecovery);
}

/** The long-run average damage multiplier from critical hits: a crit (probability `critChance`, a
 *  [0,1] value) multiplies raw damage by `critDamage` (a base-≥1 multiplier), so over many fires the
 *  expected raw damage is `raw × (1 + critChance × (critDamage − 1))`. A `critChance` of 0 (or a
 *  `critDamage` of 1) leaves it at 1. This is a DISPLAY-ONLY helper for the skill tooltip's expected
 *  damage; the live simulation rolls each crit individually (see `battleStep`), so it has no backend
 *  mirror and is outside the battle-parity contract. */
export function expectedCritMultiplier(critChance: number, critDamage: number): number {
	return 1 + critChance * (critDamage - 1);
}

/** A skill effect's magnitude after caster-attribute scaling: the authored amount plus the caster's
 *  scaling-attribute value times the per-point coefficient (`scalingAmount`). A `scalingAmount` of 0
 *  leaves the authored amount unchanged. The `attributes` are the CASTER's, mirroring how a damage
 *  multiplier scales off the caster — so e.g. the player's Dexterity strengthens a poison they apply.
 *  This is the single definition consumed by both the battle runtime (`Skill.applyEffects`) and the
 *  skill tooltip, kept bit-for-bit identical to the backend `BattleContext.ApplySkillEffect`. */
export function scaledEffectAmount(effect: ISkillEffect, attributes: BattleAttributes): number {
	return effect.amount + attributes.getValue(effect.scalingAttributeId) * effect.scalingAmount;
}
