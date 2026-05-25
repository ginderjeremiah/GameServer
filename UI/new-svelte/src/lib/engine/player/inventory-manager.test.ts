import { describe, it, expect, vi, beforeEach } from 'vitest';
import { EAttribute, EItemCategory, ELogType } from '$lib/api';
import type { IInventoryData, IItem, IItemMod } from '$lib/api';

const mockInventoryData: IInventoryData = {
	unlockedItems: [],
	unlockedMods: []
};

vi.mock('$lib/engine', () => ({
	playerManager: {
		get inventoryData() {
			return mockInventoryData;
		}
	}
}));

const mockItems: IItem[] = [];
const mockItemMods: IItemMod[] = [];

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

vi.mock('$lib/api', async (importOriginal) => {
	const actual = (await importOriginal()) as Record<string, unknown>;
	return {
		...actual,
		ApiRequest: vi.fn().mockImplementation(() => ({
			post: vi.fn().mockResolvedValue({ error: undefined })
		}))
	};
});

vi.mock('$lib/engine/log', () => ({
	logMessage: vi.fn()
}));

import { InventoryManager, EEquipmentSlot, getEquipmentSlotForCategory } from './inventory-manager';
import { logMessage } from '$lib/engine/log';

const makeItem = (id: number, category: EItemCategory = EItemCategory.Weapon): IItem => ({
	id,
	name: `Item ${id}`,
	description: `Description ${id}`,
	itemCategoryId: category,
	iconPath: `/icons/${id}.png`,
	attributes: [{ attributeId: EAttribute.Strength, amount: 5 }]
});

const makeItemMod = (id: number): IItemMod => ({
	id,
	name: `Mod ${id}`,
	removable: true,
	description: `Mod description ${id}`,
	itemModTypeId: 1,
	attributes: [{ attributeId: EAttribute.Agility, amount: 3 }]
});

describe('InventoryManager', () => {
	let manager: InventoryManager;

	beforeEach(() => {
		manager = new InventoryManager();
		vi.mocked(logMessage).mockClear();

		mockItems.length = 0;
		mockItemMods.length = 0;
		mockInventoryData.unlockedItems = [];
		mockInventoryData.unlockedMods = [];
	});

	describe('initialize', () => {
		it('loads unlocked items from player inventory data', () => {
			mockItems[1] = makeItem(1);
			mockItemMods[10] = makeItemMod(10);
			mockInventoryData.unlockedItems = [{ itemId: 1, equipped: false, appliedMods: [] }];
			mockInventoryData.unlockedMods = [10];

			manager.initialize();

			expect(manager.unlockedItems.size).toBe(1);
			expect(manager.unlockedItems.has(1)).toBe(true);
			expect(manager.unlockedMods.has(10)).toBe(true);
		});

		it('places equipped items into equipment slots', () => {
			mockItems[1] = makeItem(1);
			mockInventoryData.unlockedItems = [
				{ itemId: 1, equipped: true, equipmentSlotId: EEquipmentSlot.WeaponSlot, appliedMods: [] }
			];

			manager.initialize();

			expect(manager.equippedSlots[EEquipmentSlot.WeaponSlot]).toBeDefined();
			expect(manager.equippedSlots[EEquipmentSlot.WeaponSlot]?.itemId).toBe(1);
		});

		it('clears previous state on re-initialize', () => {
			mockItems[1] = makeItem(1);
			mockInventoryData.unlockedItems = [{ itemId: 1, equipped: false, appliedMods: [] }];
			manager.initialize();

			mockInventoryData.unlockedItems = [];
			mockInventoryData.unlockedMods = [];
			manager.initialize();

			expect(manager.unlockedItems.size).toBe(0);
			expect(manager.unlockedMods.size).toBe(0);
		});
	});

	describe('unlockedItemList', () => {
		it('returns array of all unlocked items', () => {
			mockItems[1] = makeItem(1);
			mockItems[2] = makeItem(2);
			mockInventoryData.unlockedItems = [
				{ itemId: 1, equipped: false, appliedMods: [] },
				{ itemId: 2, equipped: false, appliedMods: [] }
			];

			manager.initialize();

			expect(manager.unlockedItemList).toHaveLength(2);
		});
	});

	describe('equipmentStats', () => {
		it('returns empty array when nothing is equipped', () => {
			manager.initialize();
			expect(manager.equipmentStats).toEqual([]);
		});

		it('includes attributes from equipped items', () => {
			mockItems[1] = makeItem(1);
			mockInventoryData.unlockedItems = [
				{ itemId: 1, equipped: true, equipmentSlotId: EEquipmentSlot.WeaponSlot, appliedMods: [] }
			];

			manager.initialize();
			const stats = manager.equipmentStats;

			expect(stats.length).toBeGreaterThan(0);
		});
	});

	describe('selectedItem', () => {
		it('returns undefined when no item selected', () => {
			expect(manager.selectedItem).toBeUndefined();
		});

		it('returns the selected item', () => {
			mockItems[1] = makeItem(1);
			mockInventoryData.unlockedItems = [{ itemId: 1, equipped: false, appliedMods: [] }];
			manager.initialize();

			manager.selectItem(1);
			expect(manager.selectedItem).toBeDefined();
			expect(manager.selectedItem?.itemId).toBe(1);
		});
	});

	describe('addUnlockedItem', () => {
		it('adds a new item and logs', () => {
			mockItems[3] = makeItem(3);

			manager.addUnlockedItem({
				itemId: 3,
				equipped: false,
				appliedMods: []
			});

			expect(manager.unlockedItems.has(3)).toBe(true);
			expect(logMessage).toHaveBeenCalledWith(
				ELogType.ItemFound,
				expect.stringContaining('Item 3')
			);
		});
	});

	describe('addUnlockedMod', () => {
		it('adds a mod id and logs', () => {
			manager.addUnlockedMod(20);

			expect(manager.unlockedMods.has(20)).toBe(true);
			expect(logMessage).toHaveBeenCalledWith(ELogType.ItemFound, 'New modifier unlocked!');
		});
	});
});

describe('getEquipmentSlotForCategory', () => {
	it('maps each item category to the correct slot', () => {
		expect(getEquipmentSlotForCategory(EItemCategory.Helm)).toBe(EEquipmentSlot.HelmSlot);
		expect(getEquipmentSlotForCategory(EItemCategory.Chest)).toBe(EEquipmentSlot.ChestSlot);
		expect(getEquipmentSlotForCategory(EItemCategory.Leg)).toBe(EEquipmentSlot.LegSlot);
		expect(getEquipmentSlotForCategory(EItemCategory.Boot)).toBe(EEquipmentSlot.BootSlot);
		expect(getEquipmentSlotForCategory(EItemCategory.Weapon)).toBe(EEquipmentSlot.WeaponSlot);
		expect(getEquipmentSlotForCategory(EItemCategory.Accessory)).toBe(EEquipmentSlot.AccessorySlot);
	});
});
