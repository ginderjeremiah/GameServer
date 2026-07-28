import type { IZone } from '$lib/api';

/* Shared `IZone` contract builder (#2434). See `docs/frontend.md` → Testing Guidelines for the
   contract-drift rule.

   Excluded on purpose: the workbench's production `newItem`, and the suites seeding zone-shaped
   literals into an untyped `staticData` mock. */

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
