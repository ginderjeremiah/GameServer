import type { IEnemy } from '$lib/api';

/* Shared `IEnemy` contract builder (#2446), following the `fixtures/items.ts` / `fixtures/zones.ts`
   convention.

   The target set comes from the throwaway-required-field probe, not a `grep` — a
   `grep "IEnemy => ({"` finds three of the eight suites. Excluded on purpose:
   `lib/common/enemy-attributes.test.ts` (two builders) and
   `routes/game/screens/codex/enemy-level.test.ts` (one), which assert a partial literal `as IEnemy`.
   The cast opts them out of drift detection, so the probe never sees them and converting them is
   #2447's call, not this one's — note that issue's table lists `enemy-level.test.ts` only under
   `IZone`, so its `IEnemy` builder needs picking up there too. A `grep` still finding enemy literals
   under `src/tests/` is expected, not a gap. */

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
