import { vi } from 'vitest';
import { EActivityKey } from '$lib/api';
import type {
	PathView,
	TierView,
	WordTooltipController
} from '$routes/game/screens/proficiencies/proficiencies-lexicon';

/* Shared fixtures for the Lexicon (Proficiencies) suites (#2407). Vitest only collects
   `*.{test,spec}.{js,ts}`, so this plain `.ts` module is imported, never run as an empty suite.

   `WordDetail.test.ts` keeps its own `vi.mock('$stores', …)` — `vi.mock` is hoisted within the file it
   appears in and its factory cannot close over that file's imports — but the stub it installs is
   unrelated to anything here, so this module is a plain static import for every suite. */

/**
 * Base tier fixture — the id is required because the conlang defaults are generated from it, which is
 * what lets a suite assert on `word1`/`pron1`/`means1` without restating them per call.
 *
 * `pathOrdinal` defaults to the id so a spine built from ascending ids is ordinal-consistent. Nothing in
 * the screen actually reads it today (`TierSpine` orders by array position, reversing `path.tiers`), so
 * this is about keeping the fixture coherent rather than about any current assertion.
 */
export const tierView = (o: Partial<TierView> & { id: number }): TierView => ({
	name: `Tier ${o.id}`,
	description: '',
	pathOrdinal: o.id,
	level: 0,
	maxLevel: 10,
	xp: 0,
	xpForNext: 100,
	state: 'unlocked',
	frontier: false,
	milestoneLevels: [],
	levelModifiers: [],
	levelRewards: [],
	decipher: 'undeciphered',
	word: `word${o.id}`,
	pronunciation: `pron${o.id}`,
	translation: `means${o.id}`,
	iconPath: '',
	...o
});

/**
 * Base path fixture — an empty Pyromancy spine.
 *
 * A suite building a path *from* tiers derives `word` from the root tier itself (the rail and spine header
 * reuse the root's word, per `PathView.word`), so that derivation stays at the call site rather than
 * being guessed at here.
 */
export const pathView = (o: Partial<PathView> = {}): PathView => ({
	id: 0,
	name: 'Pyromancy',
	description: '',
	word: 'aenkor',
	iconPath: '',
	activityKey: EActivityKey.Physical,
	tiers: [],
	...o
});

/** The shared word-of-power tooltip controller the spine cards and inspector drive on hover. The id is a
 *  parameter because suites assert their own value back out of `aria-describedby`. */
export const stubController = (describedById = 'tooltip-1'): WordTooltipController => ({
	describedById,
	show: vi.fn(),
	move: vi.fn(),
	hide: vi.fn()
});
