import { describe, it, expect, afterEach } from 'vitest';
import { render, cleanup } from '@testing-library/svelte';
import { EAttribute, EItemCategory, EItemModType, ERarity } from '$lib/api';
import { BattleAttributes } from '$lib/battle/battle-attributes';
import type { Item } from '$lib/battle';
import ItemTooltip from '$routes/game/screens/inventory/ItemTooltip.svelte';

// A weapon with one prefix and one suffix mod applied. `totalAttributes` is built
// from the item's stats plus the mods' stats (Strength 5 + 3 = 8), mirroring how
// `newItem` merges them, so the rendered Stats section reflects the mod totals.
const makeItem = (): Item => {
	const totalAttributes = new BattleAttributes(
		[
			{ attributeId: EAttribute.Strength, amount: 5 },
			{ attributeId: EAttribute.Strength, amount: 3 },
			{ attributeId: EAttribute.Agility, amount: 2 }
		],
		false
	);
	return {
		id: 1,
		itemId: 1,
		name: 'Sword',
		description: 'A blade.',
		itemCategoryId: EItemCategory.Weapon,
		rarityId: ERarity.Epic,
		iconPath: '',
		tags: [],
		modSlots: [
			{ id: 10, itemId: 1, itemModSlotTypeId: EItemModType.Prefix },
			{ id: 11, itemId: 1, itemModSlotTypeId: EItemModType.Suffix }
		],
		appliedMods: [
			{
				id: 100,
				name: 'Flaming',
				description: '+3 Strength',
				itemModTypeId: EItemModType.Prefix,
				rarityId: ERarity.Legendary,
				attributes: [{ attributeId: EAttribute.Strength, amount: 3 }],
				tags: [],
				itemModSlotId: 10
			},
			{
				id: 101,
				name: 'of the Bear',
				description: '+2 Agility',
				itemModTypeId: EItemModType.Suffix,
				rarityId: ERarity.Rare,
				attributes: [{ attributeId: EAttribute.Agility, amount: 2 }],
				tags: [],
				itemModSlotId: 11
			}
		],
		equipped: false,
		favorite: false,
		totalAttributes
	} as unknown as Item;
};

afterEach(cleanup);

describe('ItemTooltip', () => {
	it('accents the tooltip border by rarity, not category', () => {
		const { container } = render(ItemTooltip, { props: { item: makeItem() } });
		const tooltip = container.querySelector('.item-tooltip') as HTMLElement;
		expect(tooltip.getAttribute('style')).toContain('var(--rarity-epic)');
		expect(tooltip.getAttribute('style')).not.toContain('var(--category-weapon)');
	});

	it('keeps the category label coloured by category', () => {
		const { container } = render(ItemTooltip, { props: { item: makeItem() } });
		const label = container.querySelector('.tt-category-label') as HTMLElement;
		expect(label.textContent).toBe('Weapon');
		expect(label.getAttribute('style')).toContain('var(--category-weapon)');
	});

	it('builds the item name from prefix and suffix mods', () => {
		const { container } = render(ItemTooltip, { props: { item: makeItem() } });
		const name = container.querySelector('.tt-item-name') as HTMLElement;
		expect(name.textContent).toBe('Flaming Sword of the Bear');
	});

	it('includes the stats contributed by applied mods', () => {
		const { container } = render(ItemTooltip, { props: { item: makeItem() } });
		const text = (container.querySelector('.tt-stats-grid') as HTMLElement).textContent ?? '';
		// Item Strength (5) + mod Strength (3) is shown as a single merged total.
		expect(text).toContain('Strength');
		expect(text).toContain('+8');
		expect(text).toContain('Agility');
		expect(text).toContain('+2');
	});

	it('accents each filled mod tile by the mod rarity', () => {
		const { container } = render(ItemTooltip, { props: { item: makeItem() } });
		const tiles = Array.from(container.querySelectorAll('.tt-mod-tile')) as HTMLElement[];
		expect(tiles).toHaveLength(2);
		expect(tiles[0].getAttribute('style')).toContain('var(--rarity-legendary)');
		expect(tiles[1].getAttribute('style')).toContain('var(--rarity-rare)');
	});
});
