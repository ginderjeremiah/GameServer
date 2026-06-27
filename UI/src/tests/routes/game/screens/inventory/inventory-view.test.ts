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
	toastError,
	unlockedMods,
	unlockedItems,
	sampleItems,
	sampleSlots,
	sampleStats,
	staticData,
	proficiencyLevels
} = vi.hoisted(() => ({
	equipItem: vi.fn(),
	unequipItem: vi.fn(),
	setFavorite: vi.fn(),
	applyMod: vi.fn(),
	removeMod: vi.fn(),
	toastError: vi.fn(),
	unlockedMods: new Set<number>(),
	// The player's per-proficiency levels the equip gate is evaluated against (kept simple: a plain map).
	proficiencyLevels: new Map<number, number>(),
	// The manager's itemId-keyed Map that selection/drag now resolve through (kept in sync with
	// sampleItems in beforeEach), mirroring InventoryManager.unlockedItems.
	unlockedItems: new Map<number, Item>(),
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
		unlockedItems,
		unlockedMods,
		equipItem,
		unequipItem,
		setFavorite,
		applyMod,
		removeMod
	}
}));

vi.mock('$stores', () => ({
	staticData,
	toastError,
	playerProficiencies: { levelOf: (id: number) => proficiencyLevels.get(id) ?? 0 }
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
	// Default the optimistic mutations to a successful persist; failure cases override per-test.
	equipItem.mockReset().mockResolvedValue(true);
	unequipItem.mockReset().mockResolvedValue(true);
	applyMod.mockReset().mockResolvedValue(true);
	removeMod.mockReset().mockResolvedValue(true);
	setFavorite.mockClear();
	toastError.mockClear();
	unlockedMods.clear();
	unlockedItems.clear();
	proficiencyLevels.clear();
	staticData.itemMods = [];
	sampleSlots.fill(undefined);
	sampleStats.length = 0;
	sampleItems.length = 0;
	sampleItems.push(
		makeItem(1, 'Zeta Helm', EItemCategory.Helm, ERarity.Rare, { favorite: true }),
		makeItem(2, 'Alpha Blade', EItemCategory.Weapon, ERarity.Legendary),
		makeItem(3, 'Mid Ring', EItemCategory.Accessory, ERarity.Common)
	);
	// Mirror the manager's itemId-keyed Map so selection/drag resolution matches the list contents.
	for (const item of sampleItems) {
		unlockedItems.set(item.itemId, item);
	}
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

describe('InventoryView filter/sort setters reset the page', () => {
	it('setSort changes the sort and snaps back to the first page', () => {
		const view = new InventoryView();
		view.page = 3;
		view.setSort('name');
		expect(view.sort).toBe('name');
		expect(view.page).toBe(0);
	});

	it('setFilterCat changes the category filter and snaps back to the first page', () => {
		const view = new InventoryView();
		view.page = 3;
		view.setFilterCat(EItemCategory.Weapon);
		expect(view.filterCat).toBe(EItemCategory.Weapon);
		expect(view.page).toBe(0);
	});

	it('setFavOnly toggles the favorites filter and snaps back to the first page', () => {
		const view = new InventoryView();
		view.page = 3;
		view.setFavOnly(true);
		expect(view.favOnly).toBe(true);
		expect(view.page).toBe(0);
	});

	it('showAll clears both filters and snaps back to the first page', () => {
		const view = new InventoryView();
		view.filterCat = EItemCategory.Weapon;
		view.favOnly = true;
		view.page = 3;
		view.showAll();
		expect(view.filterCat).toBeNull();
		expect(view.favOnly).toBe(false);
		expect(view.page).toBe(0);
	});

	// The grid clamps an out-of-range page as a safety net even when page-reset doesn't fire (the
	// visible list can shrink for reasons other than a filter/sort action). This mirrors the
	// `pageClamped = Math.min(view.page, pages - 1)` derivation in InventoryGrid.svelte.
	it('an out-of-range page still clamps into the last available page', () => {
		const view = new InventoryView();
		const perPage = 48;
		view.page = 5;
		const pages = Math.max(1, Math.ceil(view.visible.length / perPage));
		const pageClamped = Math.min(view.page, pages - 1);
		expect(view.visible.length).toBeLessThanOrEqual(perPage);
		expect(pages).toBe(1);
		expect(pageClamped).toBe(0);
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

describe('InventoryView persist-failure feedback', () => {
	it('toasts when an equip fails to persist', async () => {
		equipItem.mockResolvedValue(false);
		await new InventoryView().equip(2, 4);
		expect(toastError).toHaveBeenCalledWith('Your equipment change could not be saved. Please try again.');
	});

	it('stays silent when an equip persists', async () => {
		await new InventoryView().equip(2, 4);
		expect(toastError).not.toHaveBeenCalled();
	});

	it('toasts when an unequip fails to persist', async () => {
		unequipItem.mockResolvedValue(false);
		await new InventoryView().unequip(4);
		expect(toastError).toHaveBeenCalledWith('Your equipment change could not be saved. Please try again.');
	});

	it('toggleEquip surfaces the underlying equip failure', async () => {
		equipItem.mockResolvedValue(false);
		await new InventoryView().toggleEquip(sampleItems[1]); // unequipped weapon → equip path
		expect(toastError).toHaveBeenCalledWith('Your equipment change could not be saved. Please try again.');
	});

	it('toggleEquip surfaces the underlying unequip failure', async () => {
		unequipItem.mockResolvedValue(false);
		const equippedWeapon = makeItem(2, 'Alpha Blade', EItemCategory.Weapon, ERarity.Legendary, {
			equipped: true,
			equipmentSlotId: 4
		});
		await new InventoryView().toggleEquip(equippedWeapon);
		expect(toastError).toHaveBeenCalledWith('Your equipment change could not be saved. Please try again.');
	});

	it('toasts when applying a mod fails to persist', async () => {
		applyMod.mockResolvedValue(false);
		await new InventoryView().applyMod(2, 0, 7);
		expect(toastError).toHaveBeenCalledWith('Your modifier change could not be saved. Please try again.');
	});

	it('toasts when removing a mod fails to persist', async () => {
		removeMod.mockResolvedValue(false);
		await new InventoryView().removeMod(2, 0);
		expect(toastError).toHaveBeenCalledWith('Your modifier change could not be saved. Please try again.');
	});

	it('stays silent when a mod change persists', async () => {
		await new InventoryView().applyMod(2, 0, 7);
		await new InventoryView().removeMod(2, 0);
		expect(toastError).not.toHaveBeenCalled();
	});
});

describe('InventoryView proficiency equip gate', () => {
	it('canEquip is true for an ungated item regardless of proficiencies', () => {
		expect(new InventoryView().canEquip(sampleItems[1])).toBe(true);
	});

	it('canEquip reflects whether the player meets a gated item requirement', () => {
		const gated = makeItem(5, 'Gated Blade', EItemCategory.Weapon, ERarity.Epic, {
			requiredProficiencyId: 3,
			requiredProficiencyLevel: 5
		});
		unlockedItems.set(5, gated);

		proficiencyLevels.set(3, 4);
		expect(new InventoryView().canEquip(gated)).toBe(false);

		proficiencyLevels.set(3, 5);
		expect(new InventoryView().canEquip(gated)).toBe(true);
	});

	it('equip refuses a gated item the player has not qualified for, without calling the manager', async () => {
		const gated = makeItem(5, 'Gated Blade', EItemCategory.Weapon, ERarity.Epic, {
			requiredProficiencyId: 3,
			requiredProficiencyLevel: 5
		});
		unlockedItems.set(5, gated);
		proficiencyLevels.set(3, 2);

		await new InventoryView().equip(5, 4);

		expect(equipItem).not.toHaveBeenCalled();
		expect(toastError).toHaveBeenCalledWith('You have not met the proficiency requirement to equip this item.');
	});

	it('equip proceeds once the gate is met', async () => {
		const gated = makeItem(5, 'Gated Blade', EItemCategory.Weapon, ERarity.Epic, {
			requiredProficiencyId: 3,
			requiredProficiencyLevel: 5
		});
		unlockedItems.set(5, gated);
		proficiencyLevels.set(3, 5);

		await new InventoryView().equip(5, 4);

		expect(equipItem).toHaveBeenCalledWith(5, 4);
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
