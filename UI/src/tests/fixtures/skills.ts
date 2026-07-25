import { EDamageType, ERarity, ESkillAcquisition, type ISkill } from '$lib/api';

/* Shared `ISkill` contract builder (#2425). Two other modules build adjacent shapes and deliberately
   stay separate:

   · `lib/battle/battle-sim-test-utils.ts` (`registerSkill`) assigns ids by registry position so a
     Battler resolves a skill exactly as the live game does — it builds on this module but owns the id.
   · `routes/game/screens/fight/fight-fixtures.ts` (`makeSkill`) returns a live `Skill` domain object
     bound to an owner, not the `ISkill` contract, so it consumes this module rather than replacing it.
   · Nine suites seed untyped skill literals into a `staticData: {} as any` mock (codex, workbench
     entities/progression). They type-check as nothing, so they pay no contract-drift tax — and filling
     their previously-`undefined` fields could change what the suite renders. Left alone deliberately;
     a `grep` for skill-shaped literals under `src/tests/` still finding them is expected, not a gap. */

/**
 * Builds an {@link ISkill} reference-data entry for tests. Everything but the id is a neutral
 * placeholder so a suite states only what it asserts on.
 *
 * The conlang fields (`word`/`pronunciation`/`translation`) default to **blank**, unlike
 * {@link import('./proficiencies').makeProficiency}, which generates them from the id. A blank word is
 * what marks a skill as un-deciphered, so the synthesis reveal suites depend on the empty default —
 * generating one here would silently make every fixture skill deciphered.
 */
export const makeSkill = (overrides: Partial<ISkill> & { id: number }): ISkill => ({
	name: `Skill ${overrides.id}`,
	baseDamage: 10,
	criticalChance: 0,
	damageMultipliers: [],
	effects: [],
	damagePortions: [{ type: EDamageType.Physical, weight: 1 }],
	description: '',
	cooldownMs: 1000,
	iconPath: '',
	rarityId: ERarity.Common,
	word: '',
	pronunciation: '',
	translation: '',
	acquisition: ESkillAcquisition.Player,
	designerNotes: '',
	...overrides
});
