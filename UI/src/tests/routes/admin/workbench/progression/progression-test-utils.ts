import { vi } from 'vitest';
import { EActivityKey } from '$lib/api';
import type { ProgressionStore } from '$routes/admin/workbench/progression/progression-store.svelte';
import type { WorkbenchPath, WorkbenchProficiency } from '$routes/admin/workbench/progression/types';

/* Shared scaffolding for the progression editor's component suites (#2405). Each suite still declares
   its own `vi.mock('$stores', …)` — `vi.mock` is hoisted within the file it appears in — but the factory
   resolves this module's singletons, so the stub's shape lives in exactly one place. */

/** The last-saved `staticData` catalogues the editor's reference lookups read. */
export const staticData = {
	enemies: [] as unknown[],
	zones: [] as unknown[],
	challenges: [] as unknown[],
	items: [] as unknown[],
	classes: [] as unknown[],
	skillRecipes: [] as unknown[],
	proficiencies: [] as unknown[],
	skills: [] as unknown[],
	paths: [] as unknown[]
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
	for (const catalogue of Object.keys(staticData) as (keyof typeof staticData)[]) {
		staticData[catalogue] = [];
	}
};

/** Base path fixture — id 5, the path the detail suites open. */
export const path = (over: Partial<WorkbenchPath> = {}): WorkbenchPath => ({
	id: 5,
	name: 'Fire Path',
	description: '',
	designerNotes: '',
	activityKey: EActivityKey.Fire,
	...over
});

/**
 * Base tier fixture — id 0 on path 0, with blank conlang fields.
 *
 * A suite whose assertions depend on a different identity (TierDetail's populated conlang strings, say)
 * wraps this with its own defaults rather than changing them here; those divergences are load-bearing.
 */
export const tier = (over: Partial<WorkbenchProficiency> = {}): WorkbenchProficiency => ({
	id: 0,
	name: 'Blades',
	description: '',
	iconPath: '',
	word: '',
	pronunciation: '',
	translation: '',
	pathId: 0,
	pathOrdinal: 0,
	maxLevel: 10,
	baseXp: 100,
	xpGrowth: 1.4,
	designerNotes: '',
	levelModifiers: [],
	levelRewards: [],
	prerequisiteIds: [],
	...over
});

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
