import { EAttribute, EModifierType, type ICreatableClass } from '$lib/api';

/* Shared `ICreatableClass` contract builder (#2456), following the `fixtures/items.ts` convention.

   `ICreatableClass` is the class-creation projection the player-select flow reads, not the `IClass`
   reference-data catalogue entry; the two are separate contracts that drift independently. `IClass` has
   exactly one hand-rolled test builder (`routes/admin/workbench/references.test.ts`), so it stays a
   literal there rather than earning a builder for a single consumer — it would join this module if a
   second site appears. */

/**
 * Builds an {@link ICreatableClass} entry for tests. Everything but the id is a neutral placeholder so
 * a suite states only what it asserts on.
 *
 * The passive is inert by default — `passiveAmount`/`passiveScalingAmount` of 0 with an `Additive`
 * modifier type — because a suite asserting on the passive summary states the amounts it expects to
 * read back. `passiveScalingAttributeId` is left unset for the same reason: an unscaled passive is the
 * neutral case, and its presence is what switches the summary to its scaling phrasing.
 */
export const makeCreatableClass = (overrides: Partial<ICreatableClass> & { id: number }): ICreatableClass => ({
	name: `Class ${overrides.id}`,
	description: '',
	word: `w${overrides.id}`,
	passiveAttributeId: EAttribute.Endurance,
	passiveAmount: 0,
	passiveScalingAmount: 0,
	passiveModifierType: EModifierType.Additive,
	attributeDistributions: [],
	starterSkills: [],
	starterEquipment: [],
	...overrides
});
