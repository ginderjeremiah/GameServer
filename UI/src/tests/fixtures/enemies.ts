import type { IEnemy } from '$lib/api';

/* Shared `IEnemy` contract builder (#2446), following the `fixtures/items.ts` / `fixtures/zones.ts`
   convention.

   The target set came from the throwaway-required-field probe, not a `grep`. That probe is blind to
   one class by construction: a builder that asserts a partial literal `as IEnemy` keeps compiling
   when the contract gains a required field, so it never showed up. #2447 folded those in —
   `lib/common/enemy-attributes.test.ts` (two builders) and
   `routes/game/screens/codex/enemy-level.test.ts` (one) — so every typed `IEnemy` under `src/tests/`
   now builds through here. Reintroducing an `as IEnemy` cast re-opens that hole; state why if you do. */

/**
 * Builds an {@link IEnemy} reference-data entry for tests. Everything but the id is a neutral
 * placeholder so a suite states only what it asserts on.
 *
 * `attributeDistribution` defaults to **empty** — a zero-Toughness, zero-health enemy. That is the
 * neutral case (every derived attribute resolves to 0 at any level), so a suite whose assertions
 * turn on an enemy's resolved attributes is expected to state its own distribution rather than
 * inherit arithmetic from here. `spawns` and `skillPool` are empty and `isBoss` is false for the same
 * reason: each one flips a distinct branch (zone spawn tables, the enemy's usable skills, the boss
 * pill) when present. `retiredAt` is left unset so the fixture enemy is live.
 */
export const makeEnemy = (overrides: Partial<IEnemy> & { id: number }): IEnemy => ({
	name: `Enemy ${overrides.id}`,
	isBoss: false,
	attributeDistribution: [],
	skillPool: [],
	spawns: [],
	designerNotes: '',
	...overrides
});
