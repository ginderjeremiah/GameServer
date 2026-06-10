import { describe, it, expect, afterEach, beforeEach, vi } from 'vitest';
import { render, cleanup, screen, fireEvent } from '@testing-library/svelte';
import { ERarity, type IChallenge } from '$lib/api';

const ITEMS = [
	{ id: 0, name: 'Iron Helm', rarityId: ERarity.Common },
	{ id: 1, name: 'Dragon Blade', rarityId: ERarity.Legendary }
];
const MODS = [{ id: 0, name: 'Sharp', itemModTypeId: 2 }];
const SKILLS = [{ id: 0, name: 'Cleave', baseDamage: 12 }];

const { mockReference } = vi.hoisted(() => ({
	mockReference: {
		itemRecords: vi.fn(() => ITEMS),
		itemModRecords: vi.fn(() => MODS),
		skillRecords: vi.fn(() => SKILLS),
		itemRecName: vi.fn((id: number) => ITEMS.find((i) => i.id === id)?.name),
		itemRarityId: vi.fn((id: number) => ITEMS.find((i) => i.id === id)?.rarityId),
		rarityName: vi.fn(() => 'Legendary'),
		rarityColor: vi.fn(() => 'var(--rarity-legendary)'),
		itemModName: vi.fn((id: number) => MODS.find((m) => m.id === id)?.name),
		itemModTypeName: vi.fn(() => 'Prefix'),
		modTypeName: vi.fn(() => 'Prefix'),
		skillName: vi.fn((id: number) => SKILLS.find((s) => s.id === id)?.name),
		skillBaseDamage: vi.fn((id: number) => SKILLS.find((s) => s.id === id)?.baseDamage)
	}
}));
vi.mock('$routes/admin/workbench/reference.svelte', () => ({ reference: mockReference }));
vi.mock('$stores', () => ({ toastError: vi.fn() }));

import ChallengeRewardSection from '$routes/admin/workbench/components/challenge/ChallengeRewardSection.svelte';
import { EntityStore } from '$routes/admin/workbench/entity-store.svelte';
import type { EntityConfig, Identified } from '$routes/admin/workbench/entities/types';

const config = (): EntityConfig<Identified> =>
	({
		key: 'challenges',
		label: 'Challenges',
		singular: 'Challenge',
		glyph: 'box',
		blankName: 'Unnamed',
		newItem: (id: number) => ({ id }),
		meta: () => [],
		sections: [],
		refresh: async () => [],
		persist: async () => []
	}) as unknown as EntityConfig<Identified>;

const setup = (over: Partial<IChallenge> = {}) => {
	const challenge: IChallenge = {
		id: 1,
		name: 'Test',
		description: '',
		challengeTypeId: 1,
		entityType: 0,
		progressGoal: 10,
		...over
	} as IChallenge;
	const store = new EntityStore(config(), [challenge as unknown as Identified]);
	const record = store.items[0];
	return { store, record, baseline: store.baselineOf(1) };
};

beforeEach(() => {
	for (const fn of Object.values(mockReference)) {
		fn.mockClear();
	}
});

afterEach(cleanup);

describe('ChallengeRewardSection', () => {
	it('renders the three reward slots', () => {
		const { store, record, baseline } = setup();
		render(ChallengeRewardSection, { props: { record, baseline, store } });
		expect(screen.getByText('Item Reward')).toBeTruthy();
		expect(screen.getByText('Item Mod Reward')).toBeTruthy();
		expect(screen.getByText('Skill Reward')).toBeTruthy();
	});

	it('warns when the challenge unlocks nothing', () => {
		const { store, record, baseline } = setup();
		render(ChallengeRewardSection, { props: { record, baseline, store } });
		expect(screen.getByText(/unlocks nothing/)).toBeTruthy();
	});

	it('shows the assigned item name and drops the empty warning', () => {
		const { store, record, baseline } = setup({ rewardItemId: 1 });
		const { container } = render(ChallengeRewardSection, { props: { record, baseline, store } });
		expect((container.querySelector('.ch-reward-name') as HTMLElement).textContent).toContain('Dragon Blade');
		expect(screen.queryByText(/unlocks nothing/)).toBeNull();
	});

	it('opens the item picker and assigns the chosen item', async () => {
		const { store, record, baseline } = setup();
		const { container } = render(ChallengeRewardSection, { props: { record, baseline, store } });
		await fireEvent.click(screen.getByText('Choose item…'));
		// Picker is open: pick "Iron Helm" (id 0).
		expect(container.querySelector('.ch-picker')).toBeTruthy();
		await fireEvent.click(screen.getByText('Iron Helm'));
		expect((store.items[0] as unknown as IChallenge).rewardItemId).toBe(0);
		// Picker closes after a pick.
		expect(container.querySelector('.ch-picker')).toBeNull();
	});

	it('clears an assigned reward', async () => {
		const { store, record, baseline } = setup({ rewardItemId: 1 });
		const { container } = render(ChallengeRewardSection, { props: { record, baseline, store } });
		await fireEvent.click(container.querySelector('.row-x[title="Clear reward"]') as HTMLElement);
		expect((store.items[0] as unknown as IChallenge).rewardItemId).toBeUndefined();
	});
});
