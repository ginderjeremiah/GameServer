/* The pure scalar formulas of the battle arithmetic — one definition consumed by both the
   battle classes (`Skill.calculateDamage`, `Battler.takeDamage`, `Battler.cdMultiplier`) and
   the display surfaces (the skills page, the in-battle skill tooltip), so the numbers the UI
   shows cannot drift from the numbers the simulation fights with (#347). The battle classes
   sit under the cross-implementation parity suite, which therefore guards these formulas
   against backend drift too. */

import { EAttribute, EDamageType, type IAttributeMultiplier, type ISkill, type ISkillEffect } from '$lib/api';
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

/** Damage dealt after subtracting the defender's flat Defense and the optional flat blockReduction (a
 *  second reduction in the same clamp, supplied only when an incoming hit is blocked), never below zero. */
export function applyDefense(rawDamage: number, defense: number, blockReduction = 0): number {
	return Math.max(rawDamage - defense - blockReduction, 0);
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

/** The net damage an incoming `dealt` hit (already amplified and crit-multiplied) of `damageType` deals to a
 *  defender: percentage resistance first (`dealt × (1 − Σ applies(type).resistance)`, UNCLAMPED — a negative
 *  total amplifies as vulnerability, a total above 1 drives the result negative as absorption), then flat
 *  Defense + optional `blockReduction` last and ONLY while the post-resistance damage is still positive, so
 *  flat reduction can neither heal nor deepen an absorption heal. The resistance sum is folded in the fixed
 *  {@link resistanceAttributes} order for parity; with none authored it is 0, leaving the positive branch
 *  byte-identical to {@link applyDefense}. A negative result heals the defender. Mirrors the backend
 *  `Battler.ComputeNetDamage`. */
export function mitigateDamage(
	dealt: number,
	damageType: EDamageType,
	defenderAttributes: BattleAttributes,
	blockReduction = 0
): number {
	let resistance = 0;
	for (const attribute of resistanceAttributes(damageType)) {
		resistance += defenderAttributes.getValue(attribute);
	}
	const mitigated = dealt * (1 - resistance);
	if (mitigated <= 0) {
		// Absorption (or a zero hit): the defender takes a net heal and flat reduction never applies.
		return mitigated;
	}
	return applyDefense(mitigated, defenderAttributes.getValue(EAttribute.Defense), blockReduction);
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
