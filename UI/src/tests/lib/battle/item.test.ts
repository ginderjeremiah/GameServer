import { describe, it, expect, vi } from 'vitest';
import { EAttribute, EItemCategory, EItemModType, ERarity } from '$lib/api';
import type { IInventoryItem, IItem, IItemMod } from '$lib/api';

const { mockItems, mockItemMods } = vi.hoisted(() => ({
	mockItems: [] as IItem[],
	mockItemMods: [] as IItemMod[]
}));

vi.mock('$stores', () => ({
	staticData: {
		get items() {
			return mockItems;
		},
		get itemMods() {
			return mockItemMods;
		}
	}
}));

import { newItem } from '$lib/battle/item';

mockItems[1] = {
	id: 1,
	name: 'Sword',
	description: '',
	itemCategoryId: EItemCategory.Weapon,
	rarityId: ERarity.Epic,
	iconPath: '',
	attributes: [{ attributeId: EAttribute.Strength, amount: 5 }],
	modSlots: [],
	tags: []
};
mockItemMods[7] = {
	id: 7,
	name: 'Flaming',
	description: '',
	itemModTypeId: EItemModType.Prefix,
	rarityId: ERarity.Legendary,
	attributes: [
		{ attributeId: EAttribute.Strength, amount: 3 },
		{ attributeId: EAttribute.Agility, amount: 2 }
	],
	tags: []
};

const invItem: IInventoryItem = {
	itemId: 1,
	equipped: false,
	favorite: false,
	appliedMods: [{ itemModId: 7, itemModSlotId: 10 }]
};

describe('newItem', () => {
	it('merges the applied mods stats into totalAttributes', () => {
		const item = newItem(invItem);
		// Strength: item 5 + mod 3 = 8; Agility: mod 2.
		expect(item.totalAttributes.getValue(EAttribute.Strength)).toBe(8);
		expect(item.totalAttributes.getValue(EAttribute.Agility)).toBe(2);
	});

	it('exposes the applied mods with their slot binding', () => {
		const item = newItem(invItem);
		expect(item.appliedMods).toHaveLength(1);
		expect(item.appliedMods[0]).toMatchObject({
			name: 'Flaming',
			itemModTypeId: EItemModType.Prefix,
			itemModSlotId: 10
		});
	});
});
