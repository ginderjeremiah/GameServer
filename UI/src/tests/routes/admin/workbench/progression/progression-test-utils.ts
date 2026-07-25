import { vi } from 'vitest';
import { EActivityKey } from '$lib/api';
import type { ProgressionStore } from '$routes/admin/workbench/progression/progression-store.svelte';
import type { WorkbenchPath, WorkbenchProficiency } from '$routes/admin/workbench/progression/types';
import { makePath, makeProficiency } from '../../../../fixtures/proficiencies';

/* Shared scaffolding for the progression editor's component suites (#2405). Each suite still declares
   its own `vi.mock('$stores', …)` — `vi.mock` is hoisted within the file it appears in — but the factory
   resolves this module's singletons, so the stub's shape lives in exactly one place.

   Because the suites resolve it from inside that factory, this module is evaluated *while `$stores` is
   being mocked, so it must stay free of runtime imports that transitively reach `$stores`* — a value
   import that did would re-enter the factory resolving it. The only value imports here are `vitest`,
   `$lib/api`, and `tests/fixtures/proficiencies` (whose own only import is `$lib/api`); everything else
   is `import type` and therefore erased, which is what keeps that true today. */

const catalogueKeys = [
	'enemies',
	'zones',
	'challenges',
	'items',
	'classes',
	'skillRecipes',
	'proficiencies',
	'skills',
	'paths'
] as const;

/* The last-saved `staticData` catalogues the editor's reference lookups read. Each slot is
   `unknown[] | undefined` because the real store's is: the backing `$state` is genuinely `undefined`
   until the loading screen populates it, and that state is load-bearing (`static-data.svelte.ts`), so
   production readers guard with `?.`. A suite covering a pre-load read assigns `undefined` directly. */
export const staticData: Record<(typeof catalogueKeys)[number], unknown[] | undefined> = {
	enemies: [],
	zones: [],
	challenges: [],
	items: [],
	classes: [],
	skillRecipes: [],
	proficiencies: [],
	skills: [],
	paths: []
};

export const dangerModal = vi.fn();

/**
 * The `$stores` surface every progression suite stubs. Resolve it from an async `vi.mock` factory, which
 * may `await import(…)` even though it cannot close over the file's static imports:
 *
 * ```ts
 * vi.mock('$stores', async () => (await import('./progression-test-utils')).stubStores());
 * ```
 */
export const stubStores = () => ({ staticData, dangerModal });

/** Restores the stub to its empty, uncalled baseline. Call from `beforeEach`. */
export const resetStores = () => {
	dangerModal.mockReset();
	for (const key of catalogueKeys) {
		staticData[key] = [];
	}
};

/* `WorkbenchPath`/`WorkbenchProficiency` are plain aliases of `IPath`/`IProficiency`, so the two builders
   below are the shared contract fixtures with the workbench's divergent defaults stated on top (#2426).
   Only the divergences are restated here — everything else follows the contract fixture. */

/** Base path fixture — id 5, the path the detail suites open. */
export const path = (over: Partial<WorkbenchPath> = {}): WorkbenchPath =>
	makePath({ id: 5, name: 'Fire Path', activityKey: EActivityKey.Fire, ...over });

/**
 * Base tier fixture — id 0 on path 0, with blank conlang fields.
 *
 * The blank `iconPath`/`word`/`pronunciation`/`translation` deliberately diverge from `makeProficiency`'s
 * id-generated ones: these suites drive the *editor*, where an empty field is the authoring state under
 * test, and a generated string would quietly pre-fill inputs the detail suites assert on. `xpGrowth: 1.4`
 * likewise diverges so the XP curve is visibly non-flat.
 *
 * A suite whose assertions depend on a different identity (TierDetail's populated conlang strings, say)
 * wraps this with its own defaults rather than changing them here; those divergences are load-bearing.
 */
export const tier = (over: Partial<WorkbenchProficiency> = {}): WorkbenchProficiency =>
	makeProficiency({
		id: 0,
		name: 'Blades',
		iconPath: '',
		word: '',
		pronunciation: '',
		translation: '',
		xpGrowth: 1.4,
		...over
	});

/* The map suites (`ProgressionMap.test.ts`, `progression-map.test.ts`) build topology — several tiers
   across several paths — so identity is positional at their call sites (`mapTier(10, 0, 0)`), and their
   assertions read the generated `Path {id}` name back out of the rendered column header. The two
   adapters below keep the field set in one place while preserving both.

   `mapPath` goes to `makePath` directly rather than through `path`: the shared fixture already defaults to
   the generated `Path {id}` name and a neutral `Physical` activity, so routing through `path` would set the
   detail suites' `Fire Path`/`Fire` only to override both straight back. `mapTier` does go through `tier`,
   because it wants that builder's blank conlang fields. */

export const mapPath = (id: number, over: Partial<WorkbenchPath> = {}): WorkbenchPath => makePath({ id, ...over });

export const mapTier = (
	id: number,
	pathId: number,
	ordinal: number,
	over: Partial<WorkbenchProficiency> = {}
): WorkbenchProficiency => tier({ id, name: `Tier ${id}`, pathId, pathOrdinal: ordinal, ...over });

/**
 * Casts a partial fake to the store the components read. The suites drive these rather than the real
 * `ProgressionStore`, which only loads through a socket-backed `load()`.
 */
export const asStore = (shape: Record<string, unknown>) => shape as unknown as ProgressionStore;

/** A fake store exposing what the path-identity surfaces (`PathDetail`, its retire flow) read. */
export const makePathStore = (selectedPath: WorkbenchPath, overrides: Record<string, unknown> = {}) =>
	asStore({
		selectedPath,
		profs: [],
		paths: [],
		currentTiers: [],
		pathTab: 'identity',
		saving: false,
		pathStatus: vi.fn(() => 'clean'),
		isRetired: vi.fn(() => false),
		setPathTab: vi.fn(),
		resetPath: vi.fn(),
		retirePath: vi.fn(),
		removePath: vi.fn(),
		pathBaseline: vi.fn(() => selectedPath),
		patchPath: vi.fn(),
		...overrides
	});

/** A fake store exposing what `TierDetail` and its tab bodies read. */
export const makeTierStore = (drilledTier: WorkbenchProficiency, overrides: Record<string, unknown> = {}) =>
	asStore({
		drilledTier,
		profs: [drilledTier],
		paths: [],
		tierTab: 'identity',
		selectedPath: { name: 'Fire Path' },
		selectedLevel: 1,
		profStatus: vi.fn(() => 'clean'),
		isRetired: vi.fn(() => false),
		setTierTab: vi.fn(),
		resetProf: vi.fn(),
		retireProf: vi.fn(),
		back: vi.fn(),
		profBaseline: vi.fn(() => drilledTier),
		patchProf: vi.fn(),
		selectLevel: vi.fn(),
		updateModifier: vi.fn(),
		removeModifier: vi.fn(),
		addModifier: vi.fn(),
		setReward: vi.fn(),
		addPayout: vi.fn(),
		removePayout: vi.fn(),
		addPrerequisite: vi.fn(),
		removePrerequisite: vi.fn(),
		...overrides
	});
