import { EActivityKey, type IPath, type IProficiency } from '$lib/api';

/* Shared `IPath` / `IProficiency` contract builders (#2414). These are the raw reference-data records,
   distinct from `lexicon-test-utils.ts`'s `PathView` / `TierView` — those are the derived view models the
   Lexicon renders. A suite that needs both wants this module for the inputs and that one for the outputs. */

/**
 * Builds an {@link IProficiency} reference-data entry for tests. The conlang and icon fields are generated
 * from the id so a suite can assert on `icon-1`/`w1`/`p1`/`t1` without restating them per call; everything
 * else is a neutral placeholder. `pathId`/`pathOrdinal` default to 0 — a suite building a spine is expected
 * to place each tier explicitly rather than inherit a position.
 */
export const makeProficiency = (o: Partial<IProficiency> & { id: number }): IProficiency => ({
	name: `Prof ${o.id}`,
	description: '',
	designerNotes: '',
	iconPath: `icon-${o.id}`,
	word: `w${o.id}`,
	pronunciation: `p${o.id}`,
	translation: `t${o.id}`,
	pathId: 0,
	pathOrdinal: 0,
	maxLevel: 10,
	baseXp: 100,
	xpGrowth: 1,
	levelModifiers: [],
	levelRewards: [],
	prerequisiteIds: [],
	...o
});

/** Builds an {@link IPath} reference-data entry for tests. A path carries no word of its own — the rail and
 *  spine header reuse the root tier's — so there is nothing conlang-shaped to generate here. */
export const makePath = (o: Partial<IPath> & { id: number }): IPath => ({
	name: `Path ${o.id}`,
	description: '',
	designerNotes: '',
	activityKey: EActivityKey.Physical,
	...o
});
