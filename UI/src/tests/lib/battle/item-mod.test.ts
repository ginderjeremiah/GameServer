import { describe, it, expect, vi } from 'vitest';
import { EAttribute, EItemModType, ERarity, type IItemMod } from '$lib/api';

// `newItemMod` resolves the mod's static definition out of the in-memory
// reference cache by id (positional, mirroring `newItem` for inventory items),
// so the store is mocked here.
const { mockItemMods } = vi.hoisted(() => ({ mockItemMods: [] as IItemMod[] }));

vi.mock('$stores', () => ({
	staticData: {
		get itemMods() {
			return mockItemMods;
		}
	}
}));

import { newItemMod } from '$lib/battle/item-mod';

mockItemMods[7] = {
	id: 7,
	name: 'Flaming',
	description: 'Burns the wielder a little too',
	itemModTypeId: EItemModType.Prefix,
	rarityId: ERarity.Legendary,
	attributes: [
		{ attributeId: EAttribute.Strength, amount: 3 },
		{ attributeId: EAttribute.Agility, amount: 2 }
	],
	tags: [1, 2]
};

describe('newItemMod', () => {
	it('merges the static mod definition with the applied slot binding', () => {
		const mod = newItemMod({ itemModId: 7, itemModSlotId: 10 });
		expect(mod).toMatchObject({
			id: 7,
			name: 'Flaming',
			itemModTypeId: EItemModType.Prefix,
			rarityId: ERarity.Legendary,
			itemModSlotId: 10
		});
		expect(mod.attributes).toEqual(mockItemMods[7].attributes);
		expect(mod.tags).toEqual([1, 2]);
	});

	it('carries the slot id through without leaking the source itemModId field', () => {
		const mod = newItemMod({ itemModId: 7, itemModSlotId: 99 });
		expect(mod.itemModSlotId).toBe(99);
		// The applied model's itemModId is consumed for the lookup, not copied onto the result.
		expect('itemModId' in mod).toBe(false);
	});

	it('looks the definition up positionally by itemModId', () => {
		mockItemMods[3] = { ...mockItemMods[7], id: 3, name: 'Frosty' };
		const mod = newItemMod({ itemModId: 3, itemModSlotId: 1 });
		expect(mod.name).toBe('Frosty');
		expect(mod.id).toBe(3);
	});
});
