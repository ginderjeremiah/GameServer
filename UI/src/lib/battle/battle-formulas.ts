/* The pure scalar formulas of the battle arithmetic — one definition consumed by both the
   battle classes (`Skill.calculateDamage`, `Battler.takeDamage`, `Battler.cdMultiplier`) and
   the display surfaces (the skills page, the in-battle skill tooltip), so the numbers the UI
   shows cannot drift from the numbers the simulation fights with (#347). The battle classes
   sit under the cross-implementation parity suite, which therefore guards these formulas
   against backend drift too. */

import { EAttribute, type IAttributeMultiplier, type ISkill } from '$lib/api';
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
 *  attribute it scales. */
export function calculateSkillDamage(skill: ISkill, attributes: BattleAttributes): number {
	let damage = skill.baseDamage;
	for (const mult of skill.damageMultipliers) {
		damage += contributionValue(mult, attributes);
	}
	return damage;
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

/** Damage dealt after subtracting the defender's flat Defense, never below zero. */
export function applyDefense(rawDamage: number, defense: number): number {
	return Math.max(rawDamage - defense, 0);
}

/** The cooldown multiplier — the CooldownRecovery attribute read directly. It is a base-1 multiplier
 *  (1.0 = normal charge speed, 1.09 = +9%), so a multiplicative buff scales it intuitively. */
export function cooldownMultiplier(attributes: BattleAttributes): number {
	return attributes.getValue(EAttribute.CooldownRecovery);
}
