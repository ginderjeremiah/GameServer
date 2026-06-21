/* The pure scalar formulas of the battle arithmetic — one definition consumed by both the
   battle classes (`Skill.calculateDamage`, `Battler.takeDamage`, `Battler.cdMultiplier`) and
   the display surfaces (the skills page, the in-battle skill tooltip), so the numbers the UI
   shows cannot drift from the numbers the simulation fights with (#347). The battle classes
   sit under the cross-implementation parity suite, which therefore guards these formulas
   against backend drift too. */

import { EAttribute, type IAttributeMultiplier, type ISkill, type ISkillEffect } from '$lib/api';
import type { BattleAttributes } from './battle-attributes';

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
