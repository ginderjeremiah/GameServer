import { EItemModType, ERarity, type IItemMod } from '$lib/api';

/* Shared `IItemMod` contract builder (#2444), following the `fixtures/items.ts` convention. It stays
   a module of its own rather than joining `makeItem`: `IItem` and `IItemMod` are separate contracts
   that drift independently, even though a suite exercising applied mods usually imports both.

   The target set was confirmed with the throwaway-required-field probe, not a `grep` — only a suite
   that constructs a *typed* `IItemMod` pays the contract-drift tax. Exclusions are deliberate, in two
   classes: the production blank record (`routes/admin/workbench/entities/item-mod.ts`) and the live
   `ItemMod` domain class (`lib/battle/item-mod.ts`) are the authoritative shapes and correctly stay
   literals; `challenges/OverviewPane.test.ts` and `lib/common/challenge-unlocks.test.ts` each assert a
   deliberately partial literal `as IItemMod`, which opts them out of drift detection entirely (#2447).
   A `grep` still finding mod literals under `src/tests/` is expected, not a gap. */

/**
 * Builds an {@link IItemMod} reference-data entry for tests. Everything but the id is a neutral
 * placeholder so a suite states only what it asserts on.
 *
 * `itemModTypeId` defaults to `Component`, matching the workbench's blank record: it is the one type
 * with no naming semantics (a `Prefix`/`Suffix` affixes the host item's display name), so it stays
 * inert for suites that only care that *a* mod applied. A suite whose mod has to fit a specific
 * `modSlots` entry states the matching type explicitly, since the type is what binds mod to slot.
 * `attributes`/`tags` default empty and `retiredAt` is left unset for the same reason — an
 * unretired, statless, untagged mod is the neutral case, and each of those flips a distinct branch.
 */
export const makeItemMod = (overrides: Partial<IItemMod> & { id: number }): IItemMod => ({
	name: `Mod ${overrides.id}`,
	description: '',
	itemModTypeId: EItemModType.Component,
	rarityId: ERarity.Common,
	attributes: [],
	tags: [],
	designerNotes: '',
	...overrides
});
