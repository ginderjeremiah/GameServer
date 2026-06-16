import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, cleanup } from '@testing-library/svelte';
import { EAttribute, EItemCategory, EItemModType, ERarity, type IItemMod, type ISkill } from '$lib/api';
import { BattleAttributes } from '$lib/battle/battle-attributes';
import type { Item } from '$lib/battle';
import type { ResolvedReward } from '$routes/game/screens/challenges/challenges-view.svelte';

vi.mock('$stores', () => ({ staticData: {} }));

import RewardTooltip from '$routes/game/screens/challenges/RewardTooltip.svelte';

const previewItem = (): Item =>
	({
		id: 1,
		itemId: 1,
		name: 'Sunblade',
		description: 'Bright.',
		itemCategoryId: EItemCategory.Weapon,
		rarityId: ERarity.Legendary,
		iconPath: '',
		tags: [],
		modSlots: [{ id: 10, itemId: 1, itemModSlotTypeId: EItemModType.Prefix }],
		appliedMods: [],
		equipped: false,
		favorite: false,
		totalAttributes: new BattleAttributes([{ attributeId: EAttribute.Strength, amount: 5 }], false)
	}) as unknown as Item;

const previewMod = (): IItemMod => ({
	id: 1,
	name: 'Blazing',
	description: 'Hot.',
	itemModTypeId: EItemModType.Prefix,
	rarityId: ERarity.Epic,
	attributes: [{ attributeId: EAttribute.Strength, amount: 4 }],
	tags: []
});

const itemReward = (revealed: boolean): ResolvedReward =>
	({
		kind: 'item',
		revealed,
		rarity: ERarity.Legendary,
		accent: 'var(--rarity-legendary)',
		glow: 'var(--rarity-legendary-glow)',
		name: 'Sunblade',
		sub: 'Legendary · Weapon',
		item: previewItem()
	}) as ResolvedReward;

const modReward = (revealed: boolean): ResolvedReward =>
	({
		kind: 'mod',
		revealed,
		rarity: ERarity.Epic,
		accent: 'var(--rarity-epic)',
		glow: 'var(--rarity-epic-glow)',
		name: 'Blazing',
		sub: 'Epic · Prefix',
		mod: previewMod()
	}) as ResolvedReward;

const previewSkill = (): ISkill => ({
	id: 5,
	name: 'Firebolt',
	baseDamage: 12,
	description: 'Hurls a bolt of fire.',
	damageMultipliers: [{ attributeId: EAttribute.Intellect, multiplier: 1.5 }],
	effects: [],
	cooldownMs: 3000,
	iconPath: ''
});

const skillReward = (revealed: boolean): ResolvedReward =>
	({
		kind: 'skill',
		revealed,
		rarity: ERarity.Common,
		accent: 'var(--accent-light)',
		glow: null,
		name: 'Firebolt',
		sub: 'Skill',
		skill: previewSkill()
	}) as ResolvedReward;

afterEach(cleanup);

describe('RewardTooltip', () => {
	it('hides the container and renders nothing when there is no reward', () => {
		const { container } = render(RewardTooltip, { props: { reward: undefined } });
		const wrap = container.querySelector('.reward-tooltip') as HTMLElement;
		expect(wrap.getAttribute('style')).toContain('display: none');
		expect(container.querySelector('.tt-shell')).toBeNull();
	});

	it('renders the real item tooltip (with the name) for a revealed item reward', () => {
		const { container } = render(RewardTooltip, { props: { reward: itemReward(true) } });
		expect((container.querySelector('.tt-title-name') as HTMLElement).textContent).toBe('Sunblade');
	});

	it('renders the real mod tooltip for a revealed mod reward', () => {
		const { container } = render(RewardTooltip, { props: { reward: modReward(true) } });
		expect((container.querySelector('.tt-title-name') as HTMLElement).textContent).toBe('Blazing');
	});

	it('renders the sealed item teaser (masked name) for an unrevealed item reward', () => {
		const { container } = render(RewardTooltip, { props: { reward: itemReward(false) } });
		const name = container.querySelector('.tt-title-name') as HTMLElement;
		expect(name.textContent).toBe('?????????');
		expect(container.querySelector('.sealed-slot')).toBeTruthy();
	});

	it('renders the sealed mod teaser for an unrevealed mod reward', () => {
		const { container } = render(RewardTooltip, { props: { reward: modReward(false) } });
		expect((container.querySelector('.tt-title-name') as HTMLElement).textContent).toBe('?????????');
		expect(container.querySelector('.tt-qmark')).toBeTruthy();
	});

	it('renders the skill preview tooltip (with the name) for a revealed skill reward', () => {
		const { container } = render(RewardTooltip, { props: { reward: skillReward(true) } });
		expect((container.querySelector('.tt-title-name') as HTMLElement).textContent).toBe('Firebolt');
		expect((container.querySelector('.tt-category-label') as HTMLElement).textContent).toBe('Skill');
	});

	it('renders the sealed skill teaser (masked name) for an unrevealed skill reward', () => {
		const { container } = render(RewardTooltip, { props: { reward: skillReward(false) } });
		expect((container.querySelector('.tt-title-name') as HTMLElement).textContent).toBe('?????????');
		// One masked scaling row teases the single damage multiplier.
		expect(container.querySelector('.tt-qmark')).toBeTruthy();
	});
});
