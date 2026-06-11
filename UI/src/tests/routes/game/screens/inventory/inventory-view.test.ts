import { describe, it, expect, vi, beforeEach } from 'vitest';
import { EAttribute, EItemCategory, ERarity } from '$lib/api';
import type { Item } from '$lib/battle';
import type { IBattlerAttribute } from '$lib/api';

// Light mock of the engine so importing the view-model doesn't pull the real
// game engine. EEquipmentSlot mirrors the real enum's indices. `vi.hoisted`
// keeps these initialized before the hoisted vi.mock factory runs. The view now
// reads the manager's authoritative `unlockedItemList`/`equippedSlots`/`equipmentStats`
// and delegates every mutation, so the mock exposes that surface.
const {
	equipItem,
	unequipItem,
	setFavorite,
	applyMod,
	removeMod,
	unlockedMods,
	sampleItems,
	sampleSlots,
	sampleStats,
	staticData
} = vi.hoisted(() => ({
	equipItem: vi.fn(),
	unequipItem: vi.fn(),
	setFavorite: vi.fn(),
	applyMod: vi.fn(),
	removeMod: vi.fn(),
	unlockedMods: new Set<number>(),
	sampleItems: [] as Item[],
	sampleSlots: new Array(6).fill(undefined) as (Item | undefined)[],
	sampleStats: [] as IBattlerAttribute[],
	// eslint-disable-next-line @typescript-eslint/no-explicit-any
	staticData: { itemMods: [] as any[] | undefined }
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
		get equippedSlots() {
			return sampleSlots;
		},
		get equipmentStats() {
			return sampleStats;
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

beforeEach(() => {
	equipItem.mockClear();
	unequipItem.mockClear();
	setFavorite.mockClear();
	applyMod.mockClear();
	removeMod.mockClear();
	unlockedMods.clear();
	staticData.itemMods = [];
	sampleSlots.fill(undefined);
	sampleStats.length = 0;
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

	it('breaks a category-sort tie by name', () => {
		// Same category falls through to the localeCompare tiebreak.
		expect(SORTS.category.cmp(makeItem(9, 'Bbb', 2, 1), makeItem(8, 'Aaa', 2, 1))).toBeGreaterThan(0);
	});
});

describe('InventoryView derivations', () => {
	it('reads the unlocked items through the manager', () => {
		expect(new InventoryView().items).toHaveLength(3);
	});

	it('sorts visible items by name', () => {
		const view = new InventoryView();
		view.sort = 'name';
		expect(view.visible.map((i) => i.name)).toEqual(['Alpha Blade', 'Mid Ring', 'Zeta Helm']);
	});

	it('sorts visible items by category then name by default', () => {
		// Helm(1) < Weapon(5) < Accessory(6) by category id.
		expect(new InventoryView().visible.map((i) => i.name)).toEqual(['Zeta Helm', 'Alpha Blade', 'Mid Ring']);
	});

	it('filters by favorites and by category', () => {
		const view = new InventoryView();
		view.favOnly = true;
		expect(view.visible.map((i) => i.itemId)).toEqual([1]);

		view.favOnly = false;
		view.filterCat = EItemCategory.Weapon;
		expect(view.visible.map((i) => i.itemId)).toEqual([2]);
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

	it('yields null when the selected/drag id no longer matches an item', () => {
		const view = new InventoryView();
		view.select(999);
		expect(view.selected).toBeNull();
		view.dragItemId = 999;
		expect(view.dragItem).toBeNull();
	});

	it('derives equippedBySlot and slotsFilled from the manager slots', () => {
		sampleSlots[4] = sampleItems[1]; // Alpha Blade in the weapon slot
		const view = new InventoryView();
		expect(view.equippedBySlot[4]?.itemId).toBe(2);
		expect(view.equippedBySlot[0]).toBeUndefined();
		expect(view.slotsFilled).toBe(1);
	});

	it('projects equippedTotals from the manager equipmentStats', () => {
		sampleStats.push({ attributeId: EAttribute.Strength, amount: 5 });
		expect(new InventoryView().equippedTotals).toEqual([{ name: 'Strength', value: 5 }]);
	});
});

describe('InventoryView delegation', () => {
	it('equip delegates to the manager', () => {
		new InventoryView().equip(2, 4);
		expect(equipItem).toHaveBeenCalledWith(2, 4);
	});

	it('unequip delegates to the manager', () => {
		new InventoryView().unequip(4);
		expect(unequipItem).toHaveBeenCalledWith(4);
	});

	it('toggleEquip equips an unequipped item into its category slot', () => {
		const item = sampleItems[1]; // Weapon category (5) → slot 4
		new InventoryView().toggleEquip(item);
		expect(equipItem).toHaveBeenCalledWith(2, 4);
		expect(unequipItem).not.toHaveBeenCalled();
	});

	it('toggleEquip unequips an item already in a slot', () => {
		const item = makeItem(2, 'Alpha Blade', EItemCategory.Weapon, ERarity.Legendary, {
			equipped: true,
			equipmentSlotId: 4
		});
		new InventoryView().toggleEquip(item);
		expect(unequipItem).toHaveBeenCalledWith(4);
		expect(equipItem).not.toHaveBeenCalled();
	});

	it('toggleFavorite delegates the flipped flag to the manager', () => {
		new InventoryView().toggleFavorite(2); // currently false → true
		expect(setFavorite).toHaveBeenCalledWith(2, true);
	});

	it('toggleFavorite is a no-op for an unknown item', () => {
		new InventoryView().toggleFavorite(999);
		expect(setFavorite).not.toHaveBeenCalled();
	});

	it('applyMod delegates with the manager argument order (itemId, modId, slotId)', () => {
		new InventoryView().applyMod(2, 0 /* slotId */, 7 /* modId */);
		expect(applyMod).toHaveBeenCalledWith(2, 7, 0);
	});

	it('removeMod delegates to the manager', () => {
		new InventoryView().removeMod(2, 0);
		expect(removeMod).toHaveBeenCalledWith(2, 0);
	});
});

describe('InventoryView.compatibleMods', () => {
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

	it('skips sparse catalogue gaps and not-yet-unlocked mods', () => {
		// A gap in the itemMods catalogue must be filtered out rather than throw, and a
		// type-matching mod the player has not unlocked is excluded.
		staticData.itemMods = [
			undefined,
			{ id: 1, name: 'Heavy', itemModTypeId: 2, attributes: [] },
			{ id: 2, name: 'Locked', itemModTypeId: 2, attributes: [] }
		];
		unlockedMods.add(1); // id 2 stays locked
		const item = makeItem(2, 'Alpha Blade', EItemCategory.Weapon, ERarity.Legendary);
		expect(new InventoryView().compatibleMods(2, item).map((m) => m.id)).toEqual([1]);
	});

	it('returns nothing when the mod catalogue is unavailable', () => {
		staticData.itemMods = undefined;
		const item = makeItem(2, 'Alpha Blade', EItemCategory.Weapon, ERarity.Legendary);
		expect(new InventoryView().compatibleMods(2, item)).toEqual([]);
	});
});
