import { describe, it, expect, afterEach } from 'vitest';
import { render, cleanup } from '@testing-library/svelte';
import { EAttribute, EItemCategory, EItemModType, ERarity, type IItemMod } from '$lib/api';
import { BattleAttributes } from '$lib/battle/battle-attributes';
import type { Item } from '$lib/battle';
import SealedItemTooltip from '$routes/game/screens/challenges/SealedItemTooltip.svelte';
import SealedModTooltip from '$routes/game/screens/challenges/SealedModTooltip.svelte';
import SealedHeader from '$routes/game/screens/challenges/SealedHeader.svelte';

const makeItem = (over: Partial<Item> = {}): Item =>
	({
		id: 1,
		itemId: 1,
		name: 'Mystery Helm',
		description: 'Hidden.',
		itemCategoryId: EItemCategory.Helm,
		rarityId: ERarity.Epic,
		iconPath: '',
		tags: [],
		modSlots: [
			{ id: 10, itemId: 1, itemModSlotTypeId: EItemModType.Prefix },
			{ id: 11, itemId: 1, itemModSlotTypeId: EItemModType.Suffix }
		],
		appliedMods: [],
		equipped: false,
		favorite: false,
		totalAttributes: new BattleAttributes(
			[
				{ attributeId: EAttribute.Strength, amount: 5 },
				{ attributeId: EAttribute.Agility, amount: 2 }
			],
			false
		),
		...over
	}) as unknown as Item;

const makeMod = (over: Partial<IItemMod> = {}): IItemMod => ({
	id: 1,
	name: 'Hidden Mod',
	description: 'Secret.',
	itemModTypeId: EItemModType.Prefix,
	rarityId: ERarity.Legendary,
	attributes: [
		{ attributeId: EAttribute.Strength, amount: 5 },
		{ attributeId: EAttribute.Defense, amount: 3 }
	],
	tags: [],
	...over
});

afterEach(cleanup);

describe('SealedHeader', () => {
	it('shows the visible category/type label but masks the name', () => {
		const { container } = render(SealedHeader, {
			props: { rarityAccent: 'var(--rarity-epic)', catAccent: 'var(--category-armor)', typeLabel: 'Helm' }
		});
		expect((container.querySelector('.tt-category-label') as HTMLElement).textContent).toBe('Helm');
		const name = container.querySelector('.tt-title-name') as HTMLElement;
		expect(name.textContent).toBe('?????????');
		expect(name.classList.contains('masked')).toBe(true);
	});

	it('renders the Sealed badge accented by the rarity hue', () => {
		const { container } = render(SealedHeader, {
			props: { rarityAccent: 'var(--rarity-epic)', catAccent: 'var(--category-armor)', typeLabel: 'Helm' }
		});
		const badge = container.querySelector('.sealed-badge') as HTMLElement;
		expect(badge.textContent?.trim()).toBe('Sealed');
		expect(badge.querySelector('span')?.getAttribute('style')).toContain('var(--rarity-epic)');
	});
});

describe('SealedItemTooltip', () => {
	it('accents the panel border by the item rarity', () => {
		const { container } = render(SealedItemTooltip, { props: { item: makeItem() } });
		expect((container.querySelector('.tt-shell') as HTMLElement).getAttribute('style')).toContain('var(--rarity-epic)');
	});

	it('teases the correct number of masked stat rows without revealing values', () => {
		const { container } = render(SealedItemTooltip, { props: { item: makeItem() } });
		// Two non-zero stats → two masked rows of "???".
		expect(container.querySelectorAll('.tt-qmark')).toHaveLength(2);
		expect(container.textContent).not.toContain('Strength');
	});

	it('shows one sealed-slot row per mod slot with a 0/N count', () => {
		const { container } = render(SealedItemTooltip, { props: { item: makeItem() } });
		expect(container.querySelectorAll('.sealed-slot')).toHaveLength(2);
		expect(container.textContent).toContain('Mods · 0/2');
	});

	it('omits the stats and mods sections for a statless, slotless item', () => {
		const item = makeItem({ totalAttributes: new BattleAttributes([], false), modSlots: [] });
		const { container } = render(SealedItemTooltip, { props: { item } });
		expect(container.querySelector('.tt-qmark')).toBeNull();
		expect(container.querySelector('.sealed-slot')).toBeNull();
	});
});

describe('SealedModTooltip', () => {
	it('accents the panel border by the mod rarity', () => {
		const { container } = render(SealedModTooltip, { props: { mod: makeMod() } });
		expect((container.querySelector('.tt-shell') as HTMLElement).getAttribute('style')).toContain(
			'var(--rarity-legendary)'
		);
	});

	it('teases one masked effect row per attribute', () => {
		const { container } = render(SealedModTooltip, { props: { mod: makeMod() } });
		expect(container.querySelectorAll('.tt-qmark')).toHaveLength(2);
	});

	it('omits the effects section for a mod with no attributes', () => {
		const { container } = render(SealedModTooltip, { props: { mod: makeMod({ attributes: [] }) } });
		expect(container.querySelector('.tt-qmark')).toBeNull();
	});
});
