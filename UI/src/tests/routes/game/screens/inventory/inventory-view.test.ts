import { describe, it, expect, vi, beforeEach } from 'vitest';
import { EAttribute, EItemCategory, ERarity } from '$lib/api';
import type { Item } from '$lib/battle';

// Light mock of the engine so importing the view-model doesn't pull the real
// game engine. EEquipmentSlot mirrors the real enum's indices. `vi.hoisted`
// keeps these initialized before the hoisted vi.mock factory runs.
const { equipItem, unequipItem, setFavorite, applyMod, removeMod, unlockedMods, sampleItems, staticData } = vi.hoisted(
	() => ({
		equipItem: vi.fn(),
		unequipItem: vi.fn(),
		setFavorite: vi.fn(),
		applyMod: vi.fn(),
		removeMod: vi.fn(),
		unlockedMods: new Set<number>(),
		sampleItems: [] as Item[],
		// eslint-disable-next-line @typescript-eslint/no-explicit-any
		staticData: { itemMods: [] as any[] }
	})
);

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
		unlockedMods,
		equipItem,
		unequipItem,
		setFavorite,
		applyMod,
		removeMod
	}
}));

vi.mock('$stores', () => ({ staticData }));

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

// Locate a seeded item, narrowing it to a defined Item (avoids a null-forgiving `!`).
const itemOf = (view: InventoryView, itemId: number): Item => {
	const item = view.items.find((i) => i.itemId === itemId);
	if (!item) {
		throw new Error(`expected inventory item ${itemId} to exist`);
	}
	return item;
};

beforeEach(() => {
	equipItem.mockClear();
	unequipItem.mockClear();
	setFavorite.mockClear();
	applyMod.mockClear();
	removeMod.mockClear();
	unlockedMods.clear();
	staticData.itemMods = [];
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

	it('sorts visible items by category then name by default', () => {
		const view = new InventoryView();
		// Helm(1) < Weapon(5) < Accessory(6) by category id.
		expect(view.visible.map((i) => i.name)).toEqual(['Zeta Helm', 'Alpha Blade', 'Mid Ring']);
	});

	it('tallies overall, favorite and per-category counts', () => {
		const view = new InventoryView();
		expect(view.counts.all).toBe(3);
		expect(view.counts.fav).toBe(1);
		expect(view.counts.cats[EItemCategory.Helm]).toBe(1);
		expect(view.counts.cats[EItemCategory.Weapon]).toBe(1);
	});

	it('tracks the selected item via select()', () => {
		const view = new InventoryView();
		expect(view.selected).toBeNull();
		view.select(2);
		expect(view.selected?.name).toBe('Alpha Blade');
		view.select(null);
		expect(view.selected).toBeNull();
	});

	it('resolves the drag item from dragItemId', () => {
		const view = new InventoryView();
		expect(view.dragItem).toBeNull();
		view.dragItemId = 3;
		expect(view.dragItem?.name).toBe('Mid Ring');
	});

	it('reload re-seeds the reactive copies from the manager', () => {
		const view = new InventoryView();
		sampleItems.push(makeItem(4, 'New Boots', EItemCategory.Boot, ERarity.Common));
		expect(view.items).toHaveLength(3);
		view.reload();
		expect(view.items).toHaveLength(4);
	});

	it('swaps the occupant when equipping into an already-filled slot', () => {
		const view = new InventoryView();
		view.equip(2, 4);
		view.equip(3, 4); // re-use the same weapon slot
		expect(view.equippedBySlot[4]?.itemId).toBe(3);
		expect(view.items.find((i) => i.itemId === 2)?.equipped).toBe(false);
		expect(view.items.find((i) => i.itemId === 2)?.equipmentSlotId).toBeUndefined();
		expect(view.slotsFilled).toBe(1);
	});

	it('unequips a slot and delegates to the manager', () => {
		const view = new InventoryView();
		view.equip(2, 4);
		view.unequip(4);
		expect(view.equippedBySlot[4]).toBeUndefined();
		expect(view.items.find((i) => i.itemId === 2)?.equipped).toBe(false);
		expect(unequipItem).toHaveBeenCalledWith(4);
	});

	it('toggleEquip equips an unequipped item and unequips an equipped one', () => {
		const view = new InventoryView();
		view.toggleEquip(itemOf(view, 2)); // Weapon category (5) → slot 4
		expect(view.equippedBySlot[4]?.itemId).toBe(2);

		view.toggleEquip(itemOf(view, 2));
		expect(view.equippedBySlot[4]).toBeUndefined();
		expect(unequipItem).toHaveBeenCalledWith(4);
	});

	it('toggleFavorite is a no-op for an unknown item', () => {
		const view = new InventoryView();
		view.toggleFavorite(999);
		expect(setFavorite).not.toHaveBeenCalled();
	});

	it('lists only unlocked, type-matching mods not already applied', () => {
		staticData.itemMods = [
			{ id: 0, name: 'Sharp', itemModTypeId: 2, attributes: [] },
			{ id: 1, name: 'Heavy', itemModTypeId: 2, attributes: [] },
			{ id: 2, name: 'Quick', itemModTypeId: 3, attributes: [] }
		];
		unlockedMods.add(0);
		unlockedMods.add(1);
		// id 2 is a different type; id 1 is already on the item.
		const item = makeItem(2, 'Alpha Blade', EItemCategory.Weapon, ERarity.Legendary, {
			appliedMods: [{ id: 1, itemModSlotId: 0, attributes: [] } as unknown as Item['appliedMods'][number]]
		});
		const compatible = new InventoryView().compatibleMods(2, item);
		expect(compatible.map((m) => m.id)).toEqual([0]);
		expect(compatible[0].itemModSlotId).toBe(-1);
	});

	it('applies a mod into a slot, replacing any existing mod there, and delegates', () => {
		staticData.itemMods = [{ id: 0, name: 'Sharp', itemModTypeId: 2, attributes: [] }];
		const view = new InventoryView();
		view.applyMod(2, 0 /* slotId */, 0 /* modId */);
		const item = itemOf(view, 2);
		expect(item.appliedMods.map((m) => m.id)).toEqual([0]);
		expect(item.appliedMods[0].itemModSlotId).toBe(0);
		expect(applyMod).toHaveBeenCalledWith(2, 0, 0);
	});

	it('applyMod is a no-op when the item or mod data is missing', () => {
		staticData.itemMods = [];
		const view = new InventoryView();
		view.applyMod(2, 0, 99); // no such mod
		view.applyMod(999, 0, 0); // no such item
		expect(applyMod).not.toHaveBeenCalled();
	});

	it('removes a mod from a slot and delegates', () => {
		staticData.itemMods = [{ id: 0, name: 'Sharp', itemModTypeId: 2, attributes: [] }];
		const view = new InventoryView();
		view.applyMod(2, 0, 0);
		view.removeMod(2, 0);
		expect(view.items.find((i) => i.itemId === 2)?.appliedMods).toEqual([]);
		expect(removeMod).toHaveBeenCalledWith(2, 0);
	});

	it('removeMod is a no-op for an unknown item', () => {
		const view = new InventoryView();
		view.removeMod(999, 0);
		expect(removeMod).not.toHaveBeenCalled();
	});
});
