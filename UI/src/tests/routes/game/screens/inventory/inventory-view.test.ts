import { describe, it, expect, vi, beforeEach } from 'vitest';
import { EAttribute, EItemCategory, ERarity } from '$lib/api';
import type { Item } from '$lib/battle';

// Light mock of the engine so importing the view-model doesn't pull the real
// game engine. EEquipmentSlot mirrors the real enum's indices. `vi.hoisted`
// keeps these initialized before the hoisted vi.mock factory runs.
const { equipItem, unequipItem, setFavorite, sampleItems } = vi.hoisted(() => ({
	equipItem: vi.fn(),
	unequipItem: vi.fn(),
	setFavorite: vi.fn(),
	sampleItems: [] as Item[]
}));

vi.mock('$lib/engine', () => ({
	EEquipmentSlot: {
		HelmSlot: 0,
		ChestSlot: 1,
		LegSlot: 2,
		BootSlot: 3,
		WeaponSlot: 4,
		AccessorySlot: 5
	},
	getEquipmentSlotForCategory: (cat: number) => cat - 1,
	inventoryManager: {
		get unlockedItemList() {
			return sampleItems;
		},
		unlockedMods: new Set<number>(),
		equipItem,
		unequipItem,
		setFavorite,
		applyMod: vi.fn(),
		removeMod: vi.fn()
	}
}));

vi.mock('$stores', () => ({
	staticData: { itemMods: [] }
}));

import { itemCategoryColor, itemCategoryName } from '$lib/common';
import { InventoryView, SORTS, EQUIP_SLOTS } from '$routes/game/screens/inventory/inventory-view.svelte';

const makeItem = (itemId: number, name: string, cat: EItemCategory, rarity: ERarity, extra: Partial<Item> = {}): Item =>
	({
		id: itemId,
		itemId,
		name,
		description: '',
		itemCategoryId: cat,
		rarityId: rarity,
		iconPath: '',
		attributes: [{ attributeId: EAttribute.Strength, amount: 5 }],
		modSlots: [],
		appliedMods: [],
		equipped: false,
		favorite: false,
		totalAttributes: undefined,
		...extra
	}) as unknown as Item;

beforeEach(() => {
	equipItem.mockClear();
	unequipItem.mockClear();
	setFavorite.mockClear();
	sampleItems.length = 0;
	sampleItems.push(
		makeItem(1, 'Zeta Helm', EItemCategory.Helm, ERarity.Rare, { favorite: true }),
		makeItem(2, 'Alpha Blade', EItemCategory.Weapon, ERarity.Legendary),
		makeItem(3, 'Mid Ring', EItemCategory.Accessory, ERarity.Common)
	);
});

describe('item-category display helpers', () => {
	it('names categories from the enum', () => {
		expect(itemCategoryName(EItemCategory.Weapon)).toBe('Weapon');
	});

	it('maps categories to themeable accent vars', () => {
		expect(itemCategoryColor(EItemCategory.Helm)).toBe('var(--category-armor)');
		expect(itemCategoryColor(EItemCategory.Weapon)).toBe('var(--category-weapon)');
		expect(itemCategoryColor(EItemCategory.Accessory)).toBe('var(--category-accessory)');
	});
});

describe('inventory-view helpers', () => {
	it('declares an equip slot for every category', () => {
		expect(EQUIP_SLOTS).toHaveLength(6);
		expect(SORTS.name.cmp(makeItem(9, 'Aaa', 1, 1), makeItem(8, 'Bbb', 1, 1))).toBeLessThan(0);
	});
});

describe('InventoryView', () => {
	it('seeds reactive copies from the manager', () => {
		const view = new InventoryView();
		expect(view.items).toHaveLength(3);
	});

	it('sorts visible items by name', () => {
		const view = new InventoryView();
		view.sort = 'name';
		expect(view.visible.map((i) => i.name)).toEqual(['Alpha Blade', 'Mid Ring', 'Zeta Helm']);
	});

	it('filters by favorites and by category', () => {
		const view = new InventoryView();
		view.favOnly = true;
		expect(view.visible.map((i) => i.itemId)).toEqual([1]);

		view.favOnly = false;
		view.filterCat = EItemCategory.Weapon;
		expect(view.visible.map((i) => i.itemId)).toEqual([2]);
	});

	it('toggles favorite and delegates to the manager', () => {
		const view = new InventoryView();
		view.toggleFavorite(2);
		expect(view.items.find((i) => i.itemId === 2)?.favorite).toBe(true);
		expect(setFavorite).toHaveBeenCalledWith(2, true);
	});

	it('equips an item into a slot and reflects it in equippedBySlot + totals', () => {
		const view = new InventoryView();
		view.equip(2, 4 /* WeaponSlot */);
		expect(view.equippedBySlot[4]?.itemId).toBe(2);
		expect(equipItem).toHaveBeenCalledWith(2, 4);
		expect(view.slotsFilled).toBe(1);
		// the equipped weapon contributes +5 Strength
		expect(view.equippedTotals).toEqual([{ name: 'Strength', value: 5 }]);
	});
});
