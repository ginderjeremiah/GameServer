import { describe, it, expect, afterEach, beforeEach, vi } from 'vitest';
import { render, cleanup, screen, fireEvent } from '@testing-library/svelte';
import { ERarity, type IChallenge } from '$lib/api';

const ITEMS: { id: number; name: string; rarityId: ERarity; retiredAt?: string }[] = [
	{ id: 0, name: 'Iron Helm', rarityId: ERarity.Common },
	{ id: 1, name: 'Dragon Blade', rarityId: ERarity.Legendary },
	{ id: 2, name: 'Rusty Relic', rarityId: ERarity.Common, retiredAt: '2026-01-01T00:00:00Z' }
];
const MODS: { id: number; name: string; itemModTypeId: number; retiredAt?: string }[] = [
	{ id: 0, name: 'Sharp', itemModTypeId: 2 }
];
const SKILLS: { id: number; name: string; baseDamage: number; retiredAt?: string }[] = [
	{ id: 0, name: 'Cleave', baseDamage: 12 }
];

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
		skillBaseDamage: vi.fn((id: number) => SKILLS.find((s) => s.id === id)?.baseDamage),
		itemRetired: vi.fn((id: number) => !!ITEMS.find((i) => i.id === id)?.retiredAt),
		itemModRetired: vi.fn((id: number) => !!MODS.find((m) => m.id === id)?.retiredAt),
		skillRetired: vi.fn((id: number) => !!SKILLS.find((s) => s.id === id)?.retiredAt)
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

	it('omits a retired item from the picker for fresh authoring', async () => {
		const { store, record, baseline } = setup();
		render(ChallengeRewardSection, { props: { record, baseline, store } });
		await fireEvent.click(screen.getByText('Choose item…'));
		// Active items are offered; the retired "Rusty Relic" is not.
		expect(screen.getByText('Iron Helm')).toBeTruthy();
		expect(screen.getByText('Dragon Blade')).toBeTruthy();
		expect(screen.queryByText('Rusty Relic')).toBeNull();
	});

	it('keeps a retired current reward visible and marked, not silently blanked', () => {
		const { store, record, baseline } = setup({ rewardItemId: 2 });
		const { container } = render(ChallengeRewardSection, { props: { record, baseline, store } });
		const name = container.querySelector('.ch-reward-name') as HTMLElement;
		expect(name.textContent).toContain('Rusty Relic');
		expect(name.classList.contains('retired')).toBe(true);
		expect(container.querySelector('.ch-reward-retired')).toBeTruthy();
	});

	it('still lists the retired current reward in the picker (marked retired) so it shows as selected', async () => {
		const { store, record, baseline } = setup({ rewardItemId: 2 });
		const { container } = render(ChallengeRewardSection, { props: { record, baseline, store } });
		await fireEvent.click(screen.getByText('Change…'));
		const retiredRow = container.querySelector('.ch-picker-nm.retired') as HTMLElement;
		expect(retiredRow?.textContent).toContain('Rusty Relic');
	});

	it('opens the mod picker and assigns the chosen mod', async () => {
		const { store, record, baseline } = setup();
		const { container } = render(ChallengeRewardSection, { props: { record, baseline, store } });
		await fireEvent.click(screen.getByText('Choose mod…'));
		expect(container.querySelector('.ch-picker')).toBeTruthy();
		await fireEvent.click(screen.getByText('Sharp'));
		expect((store.items[0] as unknown as IChallenge).rewardItemModId).toBe(0);
		expect(container.querySelector('.ch-picker')).toBeNull();
	});

	it('opens the skill picker and assigns the chosen skill', async () => {
		const { store, record, baseline } = setup();
		const { container } = render(ChallengeRewardSection, { props: { record, baseline, store } });
		await fireEvent.click(screen.getByText('Choose skill…'));
		expect(container.querySelector('.ch-picker')).toBeTruthy();
		await fireEvent.click(screen.getByText('Cleave'));
		expect((store.items[0] as unknown as IChallenge).rewardSkillId).toBe(0);
		expect(container.querySelector('.ch-picker')).toBeNull();
	});

	it('collapses an open picker when the rendered record switches to a different challenge', async () => {
		// Two sibling challenges in the same store; the section is reused as the detail
		// pane switches between them, and its $effect must reset the open picker on switch.
		const first = { id: 1, name: 'One', challengeTypeId: 1, entityType: 0, progressGoal: 5 } as IChallenge;
		const second = { id: 2, name: 'Two', challengeTypeId: 1, entityType: 0, progressGoal: 5 } as IChallenge;
		const store = new EntityStore(config(), [first, second] as unknown as Identified[]);

		const { container, rerender } = render(ChallengeRewardSection, {
			props: { record: store.items[0], baseline: store.baselineOf(1), store }
		});
		await fireEvent.click(screen.getByText('Choose item…'));
		expect(container.querySelector('.ch-picker')).toBeTruthy();

		// Switch the detail pane to the other record: the picker collapses.
		await rerender({ record: store.items[1], baseline: store.baselineOf(2), store });
		expect(container.querySelector('.ch-picker')).toBeNull();
	});
});
