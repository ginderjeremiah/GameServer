import { EChallengeType, EEntityType, type IChallenge } from '$lib/api';

/* Shared `IChallenge` contract builder (#2456), following the `fixtures/items.ts` convention.

   The target set was confirmed with the throwaway-required-field probe, not a `grep`: only a suite that
   constructs a *typed* `IChallenge` pays the contract-drift tax, and a probe field flushes out four
   suites plus the production blank record (`routes/admin/workbench/entities/challenge.ts`). That record
   is the authoritative shape and correctly stays a literal. Two other classes of challenge literal are
   excluded on purpose: the untyped `staticData` mocks (the Codex suites seed `{} as any`), which see no
   new required field, and `entities/challenge.test.ts`, which spreads the production `newItem` rather
   than hand-rolling a record. A `grep` still finding challenge literals under `src/tests/` is expected,
   not a gap.

   A third class was invisible to the probe rather than excluded: a literal asserted `as IChallenge`
   keeps compiling when the contract gains a required field. #2447 handed those nine sites over here and
   they are now converted; reintroducing such a cast re-opens the hole, so state why if you do. */

/**
 * Builds an {@link IChallenge} reference-data entry for tests. Everything but the id is a neutral
 * placeholder so a suite states only what it asserts on.
 *
 * `challengeTypeId` defaults to `EnemiesKilled`, matching the workbench's blank record, but
 * `entityType` deliberately defaults to `None` rather than the `Enemy` dimension that type implies:
 * `None` is the inert scope (tracked globally, no target select, no target id), so a suite that doesn't
 * care about scope gets no scope behaviour for free. The two fields drive the condition and scope
 * surfaces, so a suite exercising either states **both** explicitly rather than leaning on these
 * defaults — as does one whose subject is the type→statistic derivation.
 *
 * That pairing is deliberately **off the production manifold**: `deriveFromType` always writes
 * `statisticType`/`entityType` from the type, so `EnemiesKilled` with `entityType: None` and no
 * statistic is a record the workbench can never author. Neutral is the point here — don't read a bare
 * `makeChallenge({ id })` as production-shaped.
 *
 * The optional fields (`statisticType`, `targetEntityId`, `rewardItemId`, `rewardItemModId`,
 * `retiredAt`) are left unset for the same reason: each flips a distinct branch — the goal unit, the
 * scope target, reward resolution, retirement filtering — when present.
 */
export const makeChallenge = (overrides: Partial<IChallenge> & { id: number }): IChallenge => ({
	name: `Challenge ${overrides.id}`,
	description: '',
	designerNotes: '',
	challengeTypeId: EChallengeType.EnemiesKilled,
	entityType: EEntityType.None,
	progressGoal: 10,
	...overrides
});
