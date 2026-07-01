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
	designerNotes: '',
	itemCategoryId: EItemCategory.Weapon,
	rarityId: ERarity.Epic,
	iconPath: '',
	requiredProficiencyLevel: 0,
	attributes: [{ attributeId: EAttribute.Strength, amount: 5 }],
	modSlots: [],
	tags: []
};
mockItemMods[7] = {
	id: 7,
	name: 'Flaming',
	description: '',
	designerNotes: '',
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
		expect(item?.totalAttributes.getValue(EAttribute.Strength)).toBe(8);
		expect(item?.totalAttributes.getValue(EAttribute.Agility)).toBe(2);
	});

	it('exposes the applied mods with their slot binding', () => {
		const item = newItem(invItem);
		expect(item?.appliedMods).toHaveLength(1);
		expect(item?.appliedMods[0]).toMatchObject({
			name: 'Flaming',
			itemModTypeId: EItemModType.Prefix,
			itemModSlotId: 10
		});
	});

	it('returns undefined for a missing/retired itemId rather than spreading undefined', () => {
		// Id 999 has no reference record (e.g. a retired or not-yet-downloaded item); resolve to undefined
		// so the inventory-load / reward-grant paths can skip it instead of crashing.
		expect(newItem({ itemId: 999, equipped: false, favorite: false, appliedMods: [] })).toBeUndefined();
	});

	it('drops an applied mod whose own reference record is missing/retired', () => {
		// One valid mod (7) and one stale mod (888): the stale one is filtered out so it can't crash the
		// item, and only the valid mod's stats are merged.
		const item = newItem({
			itemId: 1,
			equipped: false,
			favorite: false,
			appliedMods: [
				{ itemModId: 7, itemModSlotId: 10 },
				{ itemModId: 888, itemModSlotId: 11 }
			]
		});
		expect(item?.appliedMods).toHaveLength(1);
		expect(item?.appliedMods[0]).toMatchObject({ name: 'Flaming', itemModSlotId: 10 });
		// Stats reflect only the surviving mod (Strength 5 + 3 = 8; Agility 2).
		expect(item?.totalAttributes.getValue(EAttribute.Strength)).toBe(8);
		expect(item?.totalAttributes.getValue(EAttribute.Agility)).toBe(2);
	});
});
