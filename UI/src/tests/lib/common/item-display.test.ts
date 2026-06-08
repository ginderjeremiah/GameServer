import { describe, it, expect } from 'vitest';
import { EItemCategory, EItemModType, ERarity } from '$lib/api';
import {
	composeItemName,
	itemCategoryColor,
	itemCategoryName,
	modTypeColor,
	modTypeLabel
} from '$lib/common/item-display';

describe('item-display helpers', () => {
	it('maps item categories to themeable accent groups and names', () => {
		expect(itemCategoryColor(EItemCategory.Helm)).toBe('var(--category-armor)');
		expect(itemCategoryColor(EItemCategory.Boot)).toBe('var(--category-armor)');
		expect(itemCategoryColor(EItemCategory.Weapon)).toBe('var(--category-weapon)');
		expect(itemCategoryColor(EItemCategory.Accessory)).toBe('var(--category-accessory)');
		expect(itemCategoryName(EItemCategory.Weapon)).toBe('Weapon');
	});

	it('maps mod types to themeable accents and labels', () => {
		expect(modTypeColor(EItemModType.Component)).toBe('var(--mod-component)');
		expect(modTypeColor(EItemModType.Prefix)).toBe('var(--mod-prefix)');
		expect(modTypeColor(EItemModType.Suffix)).toBe('var(--mod-suffix)');
		expect(modTypeLabel(EItemModType.Prefix)).toBe('Prefix');
	});
});

describe('composeItemName', () => {
	const prefix = (name: string) => ({ name, itemModTypeId: EItemModType.Prefix });
	const suffix = (name: string) => ({ name, itemModTypeId: EItemModType.Suffix });
	const component = (name: string) => ({ name, itemModTypeId: EItemModType.Component });

	it('returns the base name when there are no mods', () => {
		expect(composeItemName('Iron Sword', [])).toBe('Iron Sword');
	});

	it('prepends prefix mod names and appends suffix mod names', () => {
		expect(composeItemName('Sword', [prefix('Flaming'), suffix('of the Bear')])).toBe('Flaming Sword of the Bear');
	});

	it('ignores component mods when building the name', () => {
		expect(composeItemName('Sword', [component('Tempered Core'), suffix('of Power')])).toBe('Sword of Power');
	});

	it('keeps applied order for multiple prefixes and suffixes', () => {
		const mods = [prefix('Flaming'), prefix('Heavy'), suffix('of Power'), suffix('of the Bear')];
		expect(composeItemName('Sword', mods)).toBe('Flaming Heavy Sword of Power of the Bear');
	});

	it('is unaffected by the rarity of the mods', () => {
		const mods = [
			{ ...prefix('Flaming'), rarityId: ERarity.Mythic },
			{ ...suffix('of Power'), rarityId: ERarity.Common }
		];
		expect(composeItemName('Sword', mods)).toBe('Flaming Sword of Power');
	});
});
