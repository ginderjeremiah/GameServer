import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { render, cleanup, screen, fireEvent, waitFor } from '@testing-library/svelte';
import { EAttribute, EModifierType } from '$lib/api';

// Hoisted so individual tests can seed staticData and assert the retire-confirm flow, mirroring
// WorkbenchDetail.test.ts's pattern for the generic Workbench's retire dialog.
const { staticData, dangerModal } = vi.hoisted(() => ({
	staticData: {
		enemies: [] as unknown[],
		zones: [] as unknown[],
		challenges: [] as unknown[],
		items: [] as unknown[],
		classes: [] as unknown[],
		skillRecipes: [] as unknown[],
		proficiencies: [] as unknown[],
		skills: [] as unknown[]
	},
	dangerModal: vi.fn()
}));
vi.mock('$stores', () => ({ staticData, dangerModal }));

import TierDetail from '$routes/admin/workbench/progression/TierDetail.svelte';
import type { ProgressionStore } from '$routes/admin/workbench/progression/progression-store.svelte';
import type { WorkbenchProficiency } from '$routes/admin/workbench/progression/types';

const tier = (over: Partial<WorkbenchProficiency> = {}): WorkbenchProficiency => ({
	id: 5,
	name: 'Blades',
	description: '',
	iconPath: 'i.png',
	word: 'sijren',
	pronunciation: 'sij-ren',
	translation: 'The Riven Frost',
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

// A fake store exposing exactly what TierDetail + ConlangIdentity (the 'identity' tab) read —
// mirrors ProgressionMap.test.ts's fake-store pattern rather than driving the real ProgressionStore
// through a socket-backed load(), since only TierDetail's own wiring (header, tabs, retire) is under
// test here.
const makeStore = (drilledTier: WorkbenchProficiency, overrides: Record<string, unknown> = {}) =>
	({
		drilledTier,
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
});
afterEach(cleanup);

describe('TierDetail', () => {
	it('renders the tier name and path/ordinal headline', () => {
		const store = makeStore(tier());
		render(TierDetail, { props: { store } });

		expect(screen.getByRole('heading', { level: 2 }).textContent).toBe('Blades');
		expect(screen.getByText(/Fire Path · Tier 0/)).toBeTruthy();
	});

	it('switches tabs through the store', async () => {
		const store = makeStore(tier());
		render(TierDetail, { props: { store } });

		await fireEvent.click(screen.getByText('XP Curve'));
		expect(store.setTierTab).toHaveBeenCalledWith('xp');
	});

	it('navigates back to the path via the breadcrumb', async () => {
		const store = makeStore(tier());
		render(TierDetail, { props: { store } });

		await fireEvent.click(screen.getByRole('button', { name: 'Fire Path' }));
		expect(store.back).toHaveBeenCalledOnce();
	});
});

describe('TierDetail — retire confirm dialog', () => {
	it('retires immediately when nothing references the tier', async () => {
		const store = makeStore(tier());
		render(TierDetail, { props: { store } });

		await fireEvent.click(screen.getByText('Retire'));

		expect(dangerModal).not.toHaveBeenCalled();
		expect(store.retireProf).toHaveBeenCalledWith(5, true);
	});

	it('prompts before retiring a tier gating an item, and retires on confirm', async () => {
		dangerModal.mockResolvedValue(true);
		staticData.items = [{ id: 0, name: 'Iron Helm', requiredProficiencyId: 5 }];

		const store = makeStore(tier());
		render(TierDetail, { props: { store } });

		await fireEvent.click(screen.getByText('Retire'));

		expect(dangerModal).toHaveBeenCalledOnce();
		const body = dangerModal.mock.calls[0][0].body as string;
		expect(body).toContain('Iron Helm');
		await waitFor(() => expect(store.retireProf).toHaveBeenCalledWith(5, true));
	});

	it('does not retire when the confirm dialog is cancelled', async () => {
		dangerModal.mockResolvedValue(false);
		staticData.items = [{ id: 0, name: 'Iron Helm', requiredProficiencyId: 5 }];

		const store = makeStore(tier());
		render(TierDetail, { props: { store } });

		await fireEvent.click(screen.getByText('Retire'));

		expect(dangerModal).toHaveBeenCalledOnce();
		expect(store.retireProf).not.toHaveBeenCalled();
	});

	it('offers Reinstate for an already-retired tier', async () => {
		const store = makeStore(tier(), { isRetired: vi.fn(() => true) });
		render(TierDetail, { props: { store } });

		expect(screen.queryByText('Retire')).toBeNull();
		await fireEvent.click(screen.getByText('Reinstate'));
		expect(store.retireProf).toHaveBeenCalledWith(5, false);
	});
});

describe('TierDetail — tab bodies', () => {
	it('renders the XP curve tab', () => {
		const store = makeStore(tier(), { tierTab: 'xp' });
		render(TierDetail, { props: { store } });

		expect(screen.getByText('Max level')).toBeTruthy();
		expect(screen.getByText(/Derived per-level cost/)).toBeTruthy();
	});

	it('renders the milestones tab and adds a payout at the selected level', async () => {
		const store = makeStore(tier(), { tierTab: 'milestones' });
		render(TierDetail, { props: { store } });

		await fireEvent.click(screen.getByTestId('progression-add-payout'));
		expect(store.addPayout).toHaveBeenCalledWith(5, 1);
	});

	it('renders an existing payout at the selected level and edits/removes it', async () => {
		const payoutTier = tier({
			levelModifiers: [
				{ level: 1, attributeId: EAttribute.Strength, modifierTypeId: EModifierType.Additive, amount: 5 }
			],
			levelRewards: [{ level: 1, rewardSkillId: 2 }]
		});
		const store = makeStore(payoutTier, { tierTab: 'milestones' });
		render(TierDetail, { props: { store } });

		expect(screen.getByText('Milestone')).toBeTruthy();
		await fireEvent.click(screen.getByLabelText('Remove modifier'));
		expect(store.removeModifier).toHaveBeenCalledWith(5, 0);

		await fireEvent.click(screen.getByText(/Remove this payout level/));
		expect(store.removePayout).toHaveBeenCalledWith(5, 1);

		await fireEvent.click(screen.getByText('+ Add modifier'));
		expect(store.addModifier).toHaveBeenCalledWith(5, 1);
	});

	it('renders the gateways tab for a root tier with no prerequisites', () => {
		const store = makeStore(tier({ pathOrdinal: 0, prerequisiteIds: [] }), { tierTab: 'gateways' });
		render(TierDetail, { props: { store } });

		expect(screen.getByText(/Prerequisite proficiencies/)).toBeTruthy();
		expect(screen.getByText(/Starter tier/)).toBeTruthy();
	});

	it('lists an existing prerequisite chip, removes it, and adds a new one', async () => {
		// staticData.proficiencies is index-addressed (the zero-based-id/index invariant), so each
		// entry must sit at its own id as the array index.
		const byId: unknown[] = [];
		byId[3] = { id: 3, name: 'Basics' };
		byId[7] = { id: 7, name: 'Advanced' };
		staticData.proficiencies = byId;
		const store = makeStore(tier({ prerequisiteIds: [3] }), { tierTab: 'gateways' });
		render(TierDetail, { props: { store } });

		expect(screen.getByText('Basics')).toBeTruthy();
		await fireEvent.click(screen.getByLabelText('Remove prerequisite'));
		expect(store.removePrerequisite).toHaveBeenCalledWith(5, 3);

		await fireEvent.change(screen.getByLabelText('Add prerequisite'), { target: { value: '7' } });
		expect(store.addPrerequisite).toHaveBeenCalledWith(5, 7);
	});
});
