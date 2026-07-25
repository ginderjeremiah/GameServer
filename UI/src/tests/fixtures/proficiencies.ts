import { EActivityKey, type IPath, type IProficiency } from '$lib/api';

/* Shared `IPath` / `IProficiency` contract builders (#2414). Two other modules build adjacent shapes:

   · `lexicon-test-utils.ts` builds the derived `PathView` / `TierView` the Lexicon renders — a suite that
     needs both wants this module for the inputs and that one for the outputs.
   · `progression/progression-test-utils.ts` (`path`/`tier`) builds `WorkbenchPath`/`WorkbenchProficiency`,
     which are *plain aliases* of these same two contracts — so a new contract field taxes that module too.
     Its blank conlang defaults and `xpGrowth: 1.4` are load-bearing for the workbench detail suites and
     deliberately diverge from the defaults here; converging the two is #2426. */

/**
 * Builds an {@link IProficiency} reference-data entry for tests. The conlang and icon fields are generated
 * from the id so a suite can assert on `icon-1`/`w1`/`p1`/`t1` without restating them per call; everything
 * else is a neutral placeholder. `pathId`/`pathOrdinal` default to 0 — a suite building a spine is expected
 * to place each tier explicitly rather than inherit a position.
 */
export const makeProficiency = (overrides: Partial<IProficiency> & { id: number }): IProficiency => ({
	name: `Prof ${overrides.id}`,
	description: '',
	designerNotes: '',
	iconPath: `icon-${overrides.id}`,
	word: `w${overrides.id}`,
	pronunciation: `p${overrides.id}`,
	translation: `t${overrides.id}`,
	pathId: 0,
	pathOrdinal: 0,
	maxLevel: 10,
	baseXp: 100,
	xpGrowth: 1,
	levelModifiers: [],
	levelRewards: [],
	prerequisiteIds: [],
	...overrides
});

/** Builds an {@link IPath} reference-data entry for tests. A path carries no word of its own — the rail and
 *  spine header reuse the root tier's — so there is nothing conlang-shaped to generate here. */
export const makePath = (overrides: Partial<IPath> & { id: number }): IPath => ({
	name: `Path ${overrides.id}`,
	description: '',
	designerNotes: '',
	activityKey: EActivityKey.Physical,
	...overrides
});
