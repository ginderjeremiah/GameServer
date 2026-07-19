import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { render, cleanup, screen, fireEvent } from '@testing-library/svelte';
import { EAttribute, EModifierType } from '$lib/api';

// MilestonesEditor's Reward-skill select reads staticData.skills via the reference module —
// mirrors TierDetail.test.ts's hoisted staticData mock.
const { staticData } = vi.hoisted(() => ({
	staticData: { skills: [] as unknown[] }
}));
vi.mock('$stores', () => ({ staticData }));

import MilestonesEditor from '$routes/admin/workbench/progression/MilestonesEditor.svelte';
import type { ProgressionStore } from '$routes/admin/workbench/progression/progression-store.svelte';
import type { WorkbenchProficiency } from '$routes/admin/workbench/progression/types';

const tier = (over: Partial<WorkbenchProficiency> = {}): WorkbenchProficiency => ({
	id: 5,
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

// A fake store exposing exactly what MilestonesEditor reads/calls — mirrors GatewaysEditor.test.ts's
// fake-store pattern rather than driving the real ProgressionStore through a socket-backed load().
const makeStore = (selectedLevel: number, overrides: Record<string, unknown> = {}) =>
	({
		selectedLevel,
		selectLevel: vi.fn(),
		updateModifier: vi.fn(),
		removeModifier: vi.fn(),
		addModifier: vi.fn(),
		setReward: vi.fn(),
		addPayout: vi.fn(),
		removePayout: vi.fn(),
		...overrides
		// eslint-disable-next-line @typescript-eslint/no-explicit-any
	}) as any as ProgressionStore;

beforeEach(() => {
	staticData.skills = [];
});
afterEach(cleanup);

describe('MilestonesEditor — level 0 (on-open) payouts (#2178)', () => {
	it('renders a level-0 node in the timeline, selectable like any other level', async () => {
		const store = makeStore(1);
		render(MilestonesEditor, { store, tier: tier() });

		const zero = screen.getByRole('button', { name: 'Level 0' });
		expect(zero).toBeTruthy();

		await fireEvent.click(zero);
		expect(store.selectLevel).toHaveBeenCalledWith(0);
	});

	it('shows the on-open empty state (not the "gain the level" copy) when level 0 has no payout', () => {
		const store = makeStore(0);
		render(MilestonesEditor, { store, tier: tier() });

		expect(screen.getByText(/players start this tier with nothing/)).toBeTruthy();
		expect(screen.queryByText(/players just gain the level/)).toBeNull();
	});

	it('lets an author add a modifier at level 0 and clears it via "Add a payout here"', async () => {
		const store = makeStore(0);
		render(MilestonesEditor, { store, tier: tier() });

		await fireEvent.click(screen.getByTestId('progression-add-payout'));
		expect(store.addPayout).toHaveBeenCalledWith(5, 0);
	});

	it('displays an existing level-0 modifier and omits the reward-skill field entirely', () => {
		const payoutTier = tier({
			levelModifiers: [
				{ level: 0, attributeId: EAttribute.Strength, modifierTypeId: EModifierType.Additive, amount: 5 }
			]
		});
		const store = makeStore(0);
		render(MilestonesEditor, { store, tier: payoutTier });

		expect(screen.getByText('Attribute modifiers')).toBeTruthy();
		// Level 0 rewards can never fire (AdminProficiencies.SetRewards rejects level < 1), so the
		// editor must not offer authoring one here.
		expect(screen.queryByText(/Reward skill/)).toBeNull();
		expect(screen.queryByLabelText('Reward skill')).toBeNull();
	});

	it('shows the reward-skill field once a level above 0 is selected', () => {
		const store = makeStore(1);
		render(MilestonesEditor, {
			store,
			tier: tier({
				levelModifiers: [
					{ level: 1, attributeId: EAttribute.Strength, modifierTypeId: EModifierType.Additive, amount: 5 }
				]
			})
		});

		expect(screen.getByText(/Reward skill/)).toBeTruthy();
	});
});
