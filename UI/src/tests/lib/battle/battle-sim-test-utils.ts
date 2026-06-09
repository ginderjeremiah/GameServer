import { Battler } from '$lib/battle';
import type { EAttribute, IAttributeMultiplier, ISkill } from '$lib/api';

/**
 * Shared helpers for the battle-simulation tests. They build the **real**
 * production {@link Battler}/{@link Skill} objects (the same ones the live
 * BattleEngine drives) from raw scenario inputs, so the tests exercise the
 * actual per-tick arithmetic instead of a hand-rolled copy.
 *
 * Each test file must `vi.mock('$stores')` with a `staticData.skills` getter
 * returning its own mock registry array, then pass that array to
 * {@link battlerFactory}; `makeBattler` registers each skill spec into it so the
 * Battler resolves its skills by id exactly as it does in the running game.
 */

/** A skill's raw definition, before it is registered and given an id. */
export interface SkillSpec {
	baseDamage: number;
	cooldownMs: number;
	multipliers: IAttributeMultiplier[];
}

export const makeSkill = (
	baseDamage: number,
	cooldownMs: number,
	multipliers: IAttributeMultiplier[] = []
): SkillSpec => ({ baseDamage, cooldownMs, multipliers });

/**
 * Binds a `makeBattler` builder to a mock skill registry (the array a test's
 * mocked `staticData.skills` returns). `makeBattler` registers each skill spec
 * into the registry, then builds a real Battler from raw stat allocations whose
 * `selectedSkills` reference those registered ids.
 */
export function battlerFactory(registry: ISkill[]) {
	return (attrs: { id: EAttribute; amount: number }[], skills: SkillSpec[]): Battler => {
		const selectedSkills = skills.map((spec) => {
			const id = registry.length;
			registry.push({
				id,
				name: `Skill ${id}`,
				baseDamage: spec.baseDamage,
				cooldownMs: spec.cooldownMs,
				damageMultipliers: spec.multipliers,
				description: '',
				iconPath: ''
			});
			return id;
		});

		return new Battler({
			name: 'Battler',
			level: 1,
			selectedSkills,
			attributes: attrs.map((a) => ({ attributeId: a.id, amount: a.amount }))
		});
	};
}
