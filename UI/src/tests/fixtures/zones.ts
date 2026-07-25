import type { IZone } from '$lib/api';

/* Shared `IZone` contract builder (#2434), following the `fixtures/skills.ts` / `fixtures/items.ts`
   convention.

   The target set is much narrower than the `grep "bossLevel:"` the issue proposed: only a suite that
   constructs a *typed* `IZone` pays the contract-drift tax, and a throwaway required field on the
   contract flushes out exactly nine suites plus the workbench's production `newItem`. Suites that
   seed zone-shaped literals into an untyped `staticData` mock are excluded on purpose — they see no
   new required field, and filling their previously-`undefined` fields could change what they render.
   A `grep` still finding zone literals under `src/tests/` is expected, not a gap. */

/**
 * Builds an {@link IZone} reference-data entry for tests. Everything but the id is a neutral
 * placeholder so a suite states only what it asserts on.
 *
 * `order` defaults to 0 rather than the id — a suite proving order-driven behaviour is expected to
 * place each zone explicitly rather than inherit a position from the Id-as-index invariant. The
 * optional FKs (`bossEnemyId`, `unlockChallengeId`) and `retiredAt` are left unset for the same
 * reason: a live, boss-less, ungated zone is the neutral case, and each of those fields flips a
 * distinct branch (the boss pill, the unlock gate, retirement filtering) when present.
 */
export const makeZone = (overrides: Partial<IZone> & { id: number }): IZone => ({
	name: `Zone ${overrides.id}`,
	description: '',
	designerNotes: '',
	order: 0,
	levelMin: 1,
	levelMax: 10,
	bossLevel: 1,
	isHome: false,
	...overrides
});
