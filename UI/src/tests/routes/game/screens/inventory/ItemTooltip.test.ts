import { describe, it, expect, afterEach } from 'vitest';
import { render, cleanup } from '@testing-library/svelte';
import { EAttribute, EItemCategory, EItemModType, ERarity, ESkillAcquisition, type ISkill } from '$lib/api';
import { BattleAttributes } from '$lib/battle/battle-attributes';
import type { Item } from '$lib/battle';
import { staticData } from '$stores';
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

const skill = (id: number, name: string): ISkill => ({
	id,
	name,
	baseDamage: 0,
	damageMultipliers: [],
	effects: [],
	description: '',
	cooldownMs: 1000,
	iconPath: '',
	rarityId: ERarity.Common,
	word: '',
	pronunciation: '',
	translation: '',
	acquisition: ESkillAcquisition.Item
});

afterEach(() => {
	cleanup();
	staticData.skills = undefined;
});

describe('ItemTooltip', () => {
	it('accents the tooltip border by rarity, not category', () => {
		const { container } = render(ItemTooltip, { props: { item: makeItem() } });
		const tooltip = container.querySelector('.tt-shell') as HTMLElement;
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
		const name = container.querySelector('.tt-title-name') as HTMLElement;
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

	it('shows the innate skill an item grants while equipped', () => {
		staticData.skills = [skill(0, 'Cleave'), skill(1, 'Fireball')];
		const item = { ...makeItem(), grantedSkillId: 1 } as unknown as Item;
		const { container } = render(ItemTooltip, { props: { item } });
		const text = container.textContent ?? '';
		expect(text).toContain('Grants');
		expect(text).toContain('Fireball');
	});

	it('omits the Grants section when the item grants no skill', () => {
		staticData.skills = [skill(0, 'Cleave')];
		const { container } = render(ItemTooltip, { props: { item: makeItem() } });
		expect(container.textContent).not.toContain('Grants');
	});

	it('omits the Grants section when the item is masked (sealed teaser)', () => {
		staticData.skills = [skill(0, 'Cleave'), skill(1, 'Fireball')];
		const item = { ...makeItem(), grantedSkillId: 1 } as unknown as Item;
		const { container } = render(ItemTooltip, { props: { item, masked: true } });
		expect(container.textContent).not.toContain('Fireball');
	});

	it('hides the panel and renders nothing while no item is hovered', () => {
		// The inventory keeps a single instance mounted (for the global tooltip to anchor
		// to) but it must stay empty and hidden until an item is hovered.
		const { container } = render(ItemTooltip, { props: { item: undefined } });
		const shell = container.querySelector('.tt-shell') as HTMLElement;
		expect(shell.style.display).toBe('none');
		expect(container.querySelector('.tt-body')).toBeNull();
		expect(container.querySelector('.tt-title-name')).toBeNull();
	});

	describe('masked', () => {
		it('accents the panel border by the item rarity', () => {
			const { container } = render(ItemTooltip, { props: { item: makeItem(), masked: true } });
			expect((container.querySelector('.tt-shell') as HTMLElement).getAttribute('style')).toContain(
				'var(--rarity-epic)'
			);
		});

		it('masks the name and shows the SEALED badge', () => {
			const { container } = render(ItemTooltip, { props: { item: makeItem(), masked: true } });
			expect((container.querySelector('.tt-title-name') as HTMLElement).textContent).toBe('?????????');
			expect((container.querySelector('.sealed-badge') as HTMLElement).textContent?.trim()).toBe('Sealed');
			expect(container.textContent).not.toContain('Flaming Sword');
		});

		it('teases the correct number of masked stat rows without revealing values', () => {
			const { container } = render(ItemTooltip, { props: { item: makeItem(), masked: true } });
			// Two non-zero stats (Strength, Agility) → two masked rows of "???".
			expect(container.querySelectorAll('.tt-qmark')).toHaveLength(2);
			expect(container.querySelector('.tt-stats-grid')).toBeNull();
			expect(container.textContent).not.toContain('Strength');
		});

		it('shows one sealed-slot row per mod slot with a redacted 0/N count', () => {
			const { container } = render(ItemTooltip, { props: { item: makeItem(), masked: true } });
			expect(container.querySelectorAll('.sealed-slot')).toHaveLength(2);
			expect(container.querySelector('.tt-mod-tile')).toBeNull();
			expect(container.textContent).toContain('Mods · 0/2');
		});

		it('always shows a masked description teaser', () => {
			const { container } = render(ItemTooltip, { props: { item: makeItem(), masked: true } });
			expect(container.textContent).toContain('Description');
			expect(container.querySelector('.tt-masked-desc')).not.toBeNull();
			expect(container.querySelector('.tt-description')).toBeNull();
		});

		it('omits the stats and mods sections for a statless, slotless item', () => {
			const item = {
				...makeItem(),
				modSlots: [],
				appliedMods: [],
				totalAttributes: new BattleAttributes([], false)
			} as unknown as Item;
			const { container } = render(ItemTooltip, { props: { item, masked: true } });
			expect(container.querySelector('.tt-qmark')).toBeNull();
			expect(container.querySelector('.sealed-slot')).toBeNull();
		});
	});
});
