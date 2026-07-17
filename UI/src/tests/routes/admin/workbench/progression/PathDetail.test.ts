import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { render, cleanup, screen, fireEvent, waitFor } from '@testing-library/svelte';
import { EActivityKey } from '$lib/api';

// Hoisted so individual tests can seed staticData and assert the retire-confirm flow, mirroring
// TierDetail.test.ts's pattern for the progression editor's retire dialog.
const { staticData, dangerModal } = vi.hoisted(() => ({
	staticData: {
		enemies: [] as unknown[],
		zones: [] as unknown[],
		challenges: [] as unknown[],
		items: [] as unknown[],
		classes: [] as unknown[],
		skillRecipes: [] as unknown[],
		proficiencies: [] as unknown[],
		skills: [] as unknown[],
		paths: [] as unknown[]
	},
	dangerModal: vi.fn()
}));
vi.mock('$stores', () => ({ staticData, dangerModal }));

import PathDetail from '$routes/admin/workbench/progression/PathDetail.svelte';
import type { ProgressionStore } from '$routes/admin/workbench/progression/progression-store.svelte';
import type { WorkbenchPath, WorkbenchProficiency } from '$routes/admin/workbench/progression/types';

const path = (over: Partial<WorkbenchPath> = {}): WorkbenchPath => ({
	id: 5,
	name: 'Fire Path',
	description: '',
	designerNotes: '',
	activityKey: EActivityKey.Fire,
	...over
});

const tier = (over: Partial<WorkbenchProficiency> = {}): WorkbenchProficiency => ({
	id: 0,
	name: 'Blades',
	description: '',
	iconPath: '',
	word: '',
	pronunciation: '',
	translation: '',
	pathId: 5,
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

// A fake store exposing exactly what PathDetail reads — mirrors TierDetail.test.ts's fake-store
// pattern rather than driving the real ProgressionStore through a socket-backed load().
const makeStore = (selectedPath: WorkbenchPath, overrides: Record<string, unknown> = {}) =>
	({
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
	}) as unknown as ProgressionStore;

beforeEach(() => {
	dangerModal.mockReset();
	staticData.enemies = [];
	staticData.zones = [];
	staticData.challenges = [];
	staticData.items = [];
	staticData.classes = [];
	staticData.skillRecipes = [];
	staticData.proficiencies = [];
	staticData.skills = [];
	staticData.paths = [];
});
afterEach(cleanup);

describe('PathDetail', () => {
	it('renders the path name', () => {
		const store = makeStore(path());
		render(PathDetail, { props: { store } });

		expect(screen.getByRole('heading', { level: 2 }).textContent).toBe('Fire Path');
	});
});

describe('PathDetail — retire confirm dialog (#1863)', () => {
	it('retires immediately when no live gateway would be soft-locked', async () => {
		const store = makeStore(path());
		render(PathDetail, { props: { store } });

		await fireEvent.click(screen.getByText('Retire'));

		expect(dangerModal).not.toHaveBeenCalled();
		expect(store.retirePath).toHaveBeenCalledWith(5, true);
	});

	it('prompts before retiring a path whose tier gates a live gateway, and retires on confirm', async () => {
		dangerModal.mockResolvedValue(true);
		// The gating edge lives only in this session's unsaved edits (store.profs), not staticData —
		// covers the second #1863 gap (unsaved-edit blindness) alongside the first (no confirm at all).
		const gatingTier = tier({ id: 6, name: 'Runeforging', pathId: 9, prerequisiteIds: [0] });
		const store = makeStore(path(), { profs: [tier(), gatingTier] });
		render(PathDetail, { props: { store } });

		await fireEvent.click(screen.getByText('Retire'));

		expect(dangerModal).toHaveBeenCalledOnce();
		const body = dangerModal.mock.calls[0][0].body as string;
		expect(body).toContain('Runeforging');
		await waitFor(() => expect(store.retirePath).toHaveBeenCalledWith(5, true));
	});

	it('does not retire when the confirm dialog is cancelled', async () => {
		dangerModal.mockResolvedValue(false);
		const gatingTier = tier({ id: 6, name: 'Runeforging', pathId: 9, prerequisiteIds: [0] });
		const store = makeStore(path(), { profs: [tier(), gatingTier] });
		render(PathDetail, { props: { store } });

		await fireEvent.click(screen.getByText('Retire'));

		expect(dangerModal).toHaveBeenCalledOnce();
		expect(store.retirePath).not.toHaveBeenCalled();
	});

	it('offers Reinstate for an already-retired path', async () => {
		const store = makeStore(path(), { isRetired: vi.fn(() => true) });
		render(PathDetail, { props: { store } });

		expect(screen.queryByText('Retire')).toBeNull();
		await fireEvent.click(screen.getByText('Reinstate'));
		expect(store.retirePath).toHaveBeenCalledWith(5, false);
	});
});

describe('PathDetail — retire confirm honours this session’s live paths, not just staticData (#2099)', () => {
	it('does not warn when the gating tier lives on a path retired only in this session (staticData still shows it live)', async () => {
		// Path 9 ("Runeforging's" path) is retired live this session but not yet saved — staticData
		// (last-saved) still shows it live, so a naive staticData-only check would wrongly warn.
		const gatingTier = tier({ id: 6, name: 'Runeforging', pathId: 9, prerequisiteIds: [0] });
		staticData.paths = [path({ id: 9, name: 'Elemental Path', activityKey: EActivityKey.Fire })];
		const liveRetiredGatingPath = path({ id: 9, name: 'Elemental Path', retiredAt: '2026-07-17T00:00:00Z' });
		const store = makeStore(path(), { profs: [tier(), gatingTier], paths: [liveRetiredGatingPath] });
		render(PathDetail, { props: { store } });

		await fireEvent.click(screen.getByText('Retire'));

		expect(dangerModal).not.toHaveBeenCalled();
		expect(store.retirePath).toHaveBeenCalledWith(5, true);
	});

	it('warns when the gating tier lives on a path reinstated only in this session (staticData still shows it retired)', async () => {
		// Inverse: staticData (last-saved) shows path 9 retired, but this session reinstated it live —
		// a naive staticData-only check would wrongly skip the warning the backend guard would still enforce.
		dangerModal.mockResolvedValue(true);
		const gatingTier = tier({ id: 6, name: 'Runeforging', pathId: 9, prerequisiteIds: [0] });
		staticData.paths = [path({ id: 9, name: 'Elemental Path', retiredAt: '2026-07-01T00:00:00Z' })];
		const liveReinstatedGatingPath = path({ id: 9, name: 'Elemental Path', retiredAt: undefined });
		const store = makeStore(path(), { profs: [tier(), gatingTier], paths: [liveReinstatedGatingPath] });
		render(PathDetail, { props: { store } });

		await fireEvent.click(screen.getByText('Retire'));

		expect(dangerModal).toHaveBeenCalledOnce();
		const body = dangerModal.mock.calls[0][0].body as string;
		expect(body).toContain('Runeforging');
		await waitFor(() => expect(store.retirePath).toHaveBeenCalledWith(5, true));
	});

	it('rebuilds a dense-by-id paths array so an unsaved new path prepended this session does not shift the gating path’s index', async () => {
		// ProgressionStore.addPath prepends new paths with negative ids (mirroring EntityStore.addItem),
		// so passing store.paths through unindexed would misalign paths[9] once a new path exists.
		const gatingTier = tier({ id: 6, name: 'Runeforging', pathId: 9, prerequisiteIds: [0] });
		const unsavedNewPath = path({ id: -1, name: 'Draft Path' });
		const liveRetiredGatingPath = path({ id: 9, name: 'Elemental Path', retiredAt: '2026-07-17T00:00:00Z' });
		const store = makeStore(path(), {
			profs: [tier(), gatingTier],
			paths: [unsavedNewPath, liveRetiredGatingPath]
		});
		render(PathDetail, { props: { store } });

		await fireEvent.click(screen.getByText('Retire'));

		expect(dangerModal).not.toHaveBeenCalled();
		expect(store.retirePath).toHaveBeenCalledWith(5, true);
	});
});
