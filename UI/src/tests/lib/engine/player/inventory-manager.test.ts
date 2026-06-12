import { describe, it, expect, vi, beforeEach } from 'vitest';
import { EAttribute, EItemCategory, EItemModType, ELogType, ERarity } from '$lib/api';
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

// Controllable stubs for the network boundary the inventory mutations cross. `mockPost` resolves
// the `{ ok }` an `ApiRequest` response exposes (mirroring `ApiResponse.ok` — a success boolean,
// since `ApiResponse.error` is an always-truthy message accessor), so a test can flip an endpoint to
// the error/rollback path; `mockSendSocketCommand` backs `setFavorite`'s websocket persistence.
const { mockPost, mockSendSocketCommand } = vi.hoisted(() => {
	const mockPost = vi.fn();
	const mockSendSocketCommand = vi.fn();
	return { mockPost, mockSendSocketCommand };
});

vi.mock('$lib/api', async (importOriginal) => {
	const actual = (await importOriginal()) as Record<string, unknown>;
	return {
		...actual,
		ApiRequest: vi.fn().mockImplementation(function (
			this: { endpoint: string; post: typeof mockPost },
			endpoint: string
		) {
			this.endpoint = endpoint;
			this.post = mockPost;
		}),
		apiSocket: {
			sendSocketCommand: mockSendSocketCommand
		}
	};
});

vi.mock('$lib/engine/log', () => ({
	logMessage: vi.fn()
}));

import { InventoryManager, EEquipmentSlot, getEquipmentSlotForCategory } from '$lib/engine/player/inventory-manager';
import { ApiRequest } from '$lib/api';
import { logMessage } from '$lib/engine/log';
import type { IInventoryItem } from '$lib/api';

const makeItem = (id: number, category: EItemCategory = EItemCategory.Weapon): IItem => ({
	id,
	name: `Item ${id}`,
	description: `Description ${id}`,
	itemCategoryId: category,
	rarityId: ERarity.Common,
	iconPath: `/icons/${id}.png`,
	attributes: [{ attributeId: EAttribute.Strength, amount: 5 }],
	modSlots: [],
	tags: []
});

const makeItemMod = (id: number): IItemMod => ({
	id,
	name: `Mod ${id}`,
	description: `Mod description ${id}`,
	itemModTypeId: EItemModType.Component,
	rarityId: ERarity.Uncommon,
	attributes: [{ attributeId: EAttribute.Agility, amount: 3 }],
	tags: []
});

const makeInventoryItem = (overrides: Partial<IInventoryItem> & { itemId: number }): IInventoryItem => ({
	equipped: false,
	favorite: false,
	appliedMods: [],
	...overrides
});

describe('InventoryManager', () => {
	let manager: InventoryManager;

	beforeEach(() => {
		manager = new InventoryManager();
		vi.mocked(logMessage).mockClear();
		vi.mocked(ApiRequest).mockClear();

		// Default the network boundary to success; error-path tests override per case.
		mockPost.mockReset().mockResolvedValue({ ok: true });
		mockSendSocketCommand.mockReset().mockResolvedValue(undefined);

		mockItems.length = 0;
		mockItemMods.length = 0;
		mockInventoryData.unlockedItems = [];
		mockInventoryData.unlockedMods = [];
	});

	describe('initialize', () => {
		it('loads unlocked items from player inventory data', () => {
			mockItems[1] = makeItem(1);
			mockItemMods[10] = makeItemMod(10);
			mockInventoryData.unlockedItems = [{ itemId: 1, equipped: false, appliedMods: [], favorite: false }];
			mockInventoryData.unlockedMods = [10];

			manager.initialize();

			expect(manager.unlockedItems.size).toBe(1);
			expect(manager.unlockedItems.has(1)).toBe(true);
			expect(manager.unlockedMods.has(10)).toBe(true);
		});

		it('places equipped items into equipment slots', () => {
			mockItems[1] = makeItem(1);
			mockInventoryData.unlockedItems = [
				{ itemId: 1, equipped: true, equipmentSlotId: EEquipmentSlot.WeaponSlot, appliedMods: [], favorite: false }
			];

			manager.initialize();

			expect(manager.equippedSlots[EEquipmentSlot.WeaponSlot]).toBeDefined();
			expect(manager.equippedSlots[EEquipmentSlot.WeaponSlot]?.itemId).toBe(1);
		});

		it('clears previous state on re-initialize', () => {
			mockItems[1] = makeItem(1);
			mockInventoryData.unlockedItems = [{ itemId: 1, equipped: false, appliedMods: [], favorite: false }];
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
				{ itemId: 1, equipped: false, appliedMods: [], favorite: false },
				{ itemId: 2, equipped: false, appliedMods: [], favorite: false }
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
				{ itemId: 1, equipped: true, equipmentSlotId: EEquipmentSlot.WeaponSlot, appliedMods: [], favorite: false }
			];

			manager.initialize();
			const stats = manager.equipmentStats;

			expect(stats.length).toBeGreaterThan(0);
		});

		it('merges equipped item attributes with their applied mods across slots', () => {
			mockItems[1] = makeItem(1, EItemCategory.Weapon);
			mockItems[2] = makeItem(2, EItemCategory.Helm);
			mockItemMods[10] = makeItemMod(10);
			mockInventoryData.unlockedItems = [
				makeInventoryItem({
					itemId: 1,
					equipped: true,
					equipmentSlotId: EEquipmentSlot.WeaponSlot,
					appliedMods: [{ itemModId: 10, itemModSlotId: 0 }]
				}),
				makeInventoryItem({ itemId: 2, equipped: true, equipmentSlotId: EEquipmentSlot.HelmSlot })
			];

			manager.initialize();
			const stats = manager.equipmentStats;

			// Slots aggregate in index order: Helm (item 2) before Weapon (item 1 + its mod).
			expect(stats).toEqual([
				{ attributeId: EAttribute.Strength, amount: 5 },
				{ attributeId: EAttribute.Strength, amount: 5 },
				{ attributeId: EAttribute.Agility, amount: 3 }
			]);
		});
	});

	describe('items (reactive published list)', () => {
		it('mirrors unlockedItemList and republishes a new array reference on mutation', async () => {
			mockItems[1] = makeItem(1);
			mockInventoryData.unlockedItems = [makeInventoryItem({ itemId: 1 })];
			manager.initialize();

			expect(manager.items).toBe(manager.unlockedItemList);
			expect(manager.items.map((i) => i.itemId)).toEqual([1]);

			const before = manager.items;
			await manager.equipItem(1, EEquipmentSlot.WeaponSlot);
			// A mutation publishes a fresh array reference so reactive consumers re-derive.
			expect(manager.items).not.toBe(before);
		});

		it('publishes a newly unlocked item into the list', () => {
			mockItems[3] = makeItem(3);
			manager.initialize();

			manager.addUnlockedItem(makeInventoryItem({ itemId: 3 }));

			expect(manager.items.map((i) => i.itemId)).toEqual([3]);
		});
	});

	describe('equipItem', () => {
		it('equips an item into an empty slot and posts to the API', async () => {
			mockItems[1] = makeItem(1);
			mockInventoryData.unlockedItems = [makeInventoryItem({ itemId: 1 })];
			manager.initialize();

			const result = await manager.equipItem(1, EEquipmentSlot.WeaponSlot);

			expect(result).toBe(true);
			expect(ApiRequest).toHaveBeenCalledWith('Player/EquipItem');
			expect(mockPost).toHaveBeenCalledWith({ itemId: 1, equipmentSlotId: EEquipmentSlot.WeaponSlot });
			const equipped = manager.equippedSlots[EEquipmentSlot.WeaponSlot];
			expect(equipped?.itemId).toBe(1);
			expect(equipped?.equipped).toBe(true);
			expect(equipped?.equipmentSlotId).toBe(EEquipmentSlot.WeaponSlot);
		});

		it('displaces the item already occupying the target slot', async () => {
			mockItems[1] = makeItem(1);
			mockItems[2] = makeItem(2);
			mockInventoryData.unlockedItems = [
				makeInventoryItem({ itemId: 1, equipped: true, equipmentSlotId: EEquipmentSlot.WeaponSlot }),
				makeInventoryItem({ itemId: 2 })
			];
			manager.initialize();
			const displaced = manager.unlockedItems.get(1);

			const result = await manager.equipItem(2, EEquipmentSlot.WeaponSlot);

			expect(result).toBe(true);
			expect(manager.equippedSlots[EEquipmentSlot.WeaponSlot]?.itemId).toBe(2);
			expect(displaced?.equipped).toBe(false);
			expect(displaced?.equipmentSlotId).toBeUndefined();
		});

		it('moves an item already equipped in another slot', async () => {
			mockItems[1] = makeItem(1);
			mockInventoryData.unlockedItems = [
				makeInventoryItem({ itemId: 1, equipped: true, equipmentSlotId: EEquipmentSlot.HelmSlot })
			];
			manager.initialize();

			const result = await manager.equipItem(1, EEquipmentSlot.ChestSlot);

			expect(result).toBe(true);
			expect(manager.equippedSlots[EEquipmentSlot.HelmSlot]).toBeUndefined();
			expect(manager.equippedSlots[EEquipmentSlot.ChestSlot]?.itemId).toBe(1);
			expect(manager.equippedSlots[EEquipmentSlot.ChestSlot]?.equipmentSlotId).toBe(EEquipmentSlot.ChestSlot);
		});

		it('returns false and makes no API call for an unknown item', async () => {
			manager.initialize();

			const result = await manager.equipItem(99, EEquipmentSlot.WeaponSlot);

			expect(result).toBe(false);
			expect(ApiRequest).not.toHaveBeenCalled();
			expect(manager.equippedSlots[EEquipmentSlot.WeaponSlot]).toBeUndefined();
		});

		it('leaves state unchanged when the API returns an error', async () => {
			mockItems[1] = makeItem(1);
			mockInventoryData.unlockedItems = [makeInventoryItem({ itemId: 1 })];
			manager.initialize();
			mockPost.mockResolvedValue({ ok: false, error: 'nope' });

			const result = await manager.equipItem(1, EEquipmentSlot.WeaponSlot);

			expect(result).toBe(false);
			expect(manager.equippedSlots[EEquipmentSlot.WeaponSlot]).toBeUndefined();
			expect(manager.unlockedItems.get(1)?.equipped).toBe(false);
		});

		it('applies the change optimistically before the persist resolves, then rolls back on error', async () => {
			mockItems[1] = makeItem(1);
			mockInventoryData.unlockedItems = [makeInventoryItem({ itemId: 1 })];
			manager.initialize();

			let resolvePost: (value: { ok: boolean; error?: string }) => void = () => {};
			mockPost.mockReturnValue(new Promise((resolve) => (resolvePost = resolve)));

			const pending = manager.equipItem(1, EEquipmentSlot.WeaponSlot);
			// Optimistically equipped while the persist is still in flight.
			expect(manager.equippedSlots[EEquipmentSlot.WeaponSlot]?.itemId).toBe(1);
			expect(manager.unlockedItems.get(1)?.equipped).toBe(true);

			resolvePost({ ok: false, error: 'nope' });
			await pending;

			expect(manager.equippedSlots[EEquipmentSlot.WeaponSlot]).toBeUndefined();
			expect(manager.unlockedItems.get(1)?.equipped).toBe(false);
		});
	});

	describe('unequipItem', () => {
		it('unequips the item in a slot and posts to the API', async () => {
			mockItems[1] = makeItem(1);
			mockInventoryData.unlockedItems = [
				makeInventoryItem({ itemId: 1, equipped: true, equipmentSlotId: EEquipmentSlot.WeaponSlot })
			];
			manager.initialize();
			const item = manager.unlockedItems.get(1);

			const result = await manager.unequipItem(EEquipmentSlot.WeaponSlot);

			expect(result).toBe(true);
			expect(ApiRequest).toHaveBeenCalledWith('Player/UnequipItem');
			expect(mockPost).toHaveBeenCalledWith({ itemId: 1, equipmentSlotId: EEquipmentSlot.WeaponSlot });
			expect(manager.equippedSlots[EEquipmentSlot.WeaponSlot]).toBeUndefined();
			expect(item?.equipped).toBe(false);
			expect(item?.equipmentSlotId).toBeUndefined();
		});

		it('returns false and makes no API call for an empty slot', async () => {
			manager.initialize();

			const result = await manager.unequipItem(EEquipmentSlot.WeaponSlot);

			expect(result).toBe(false);
			expect(ApiRequest).not.toHaveBeenCalled();
		});

		it('leaves the item equipped when the API returns an error', async () => {
			mockItems[1] = makeItem(1);
			mockInventoryData.unlockedItems = [
				makeInventoryItem({ itemId: 1, equipped: true, equipmentSlotId: EEquipmentSlot.WeaponSlot })
			];
			manager.initialize();
			mockPost.mockResolvedValue({ ok: false, error: 'nope' });

			const result = await manager.unequipItem(EEquipmentSlot.WeaponSlot);

			expect(result).toBe(false);
			expect(manager.equippedSlots[EEquipmentSlot.WeaponSlot]?.itemId).toBe(1);
		});
	});

	describe('applyMod', () => {
		it('posts to the API and logs on success', async () => {
			mockItems[1] = makeItem(1);
			mockItemMods[10] = makeItemMod(10);
			mockInventoryData.unlockedItems = [makeInventoryItem({ itemId: 1 })];
			mockInventoryData.unlockedMods = [10];
			manager.initialize();

			const result = await manager.applyMod(1, 10, 0);

			expect(result).toBe(true);
			expect(ApiRequest).toHaveBeenCalledWith('Player/ApplyMod');
			expect(mockPost).toHaveBeenCalledWith({ itemId: 1, itemModId: 10, itemModSlotId: 0 });
			expect(logMessage).toHaveBeenCalledWith(ELogType.ItemFound, 'Modifier applied.');
		});

		it('mirrors the mod onto the authoritative item and its totalAttributes on success', async () => {
			mockItems[1] = makeItem(1);
			mockItemMods[10] = makeItemMod(10);
			mockInventoryData.unlockedItems = [makeInventoryItem({ itemId: 1 })];
			mockInventoryData.unlockedMods = [10];
			manager.initialize();

			await manager.applyMod(1, 10, 0);

			const item = manager.unlockedItems.get(1);
			expect(item?.appliedMods.map((m) => m.id)).toEqual([10]);
			expect(item?.appliedMods[0].itemModSlotId).toBe(0);
			// totalAttributes recomputes to include the mod's attributes (Agility 3).
			expect(item?.totalAttributes.getValue(EAttribute.Agility)).toBe(3);
		});

		it('replaces an existing mod occupying the same slot', async () => {
			mockItems[1] = makeItem(1);
			mockItemMods[10] = makeItemMod(10);
			mockItemMods[11] = makeItemMod(11);
			mockInventoryData.unlockedItems = [
				makeInventoryItem({ itemId: 1, appliedMods: [{ itemModId: 10, itemModSlotId: 0 }] })
			];
			mockInventoryData.unlockedMods = [10, 11];
			manager.initialize();

			await manager.applyMod(1, 11, 0);

			const item = manager.unlockedItems.get(1);
			expect(item?.appliedMods.map((m) => m.id)).toEqual([11]);
		});

		it('reflects an applied mod in equipmentStats for an equipped item', async () => {
			mockItems[1] = makeItem(1);
			mockItemMods[10] = makeItemMod(10);
			mockInventoryData.unlockedItems = [
				makeInventoryItem({ itemId: 1, equipped: true, equipmentSlotId: EEquipmentSlot.WeaponSlot })
			];
			mockInventoryData.unlockedMods = [10];
			manager.initialize();

			await manager.applyMod(1, 10, 0);

			expect(manager.equipmentStats).toEqual([
				{ attributeId: EAttribute.Strength, amount: 5 },
				{ attributeId: EAttribute.Agility, amount: 3 }
			]);
		});

		it('leaves the authoritative item unchanged when the API returns an error', async () => {
			mockItems[1] = makeItem(1);
			mockItemMods[10] = makeItemMod(10);
			mockInventoryData.unlockedItems = [makeInventoryItem({ itemId: 1 })];
			mockInventoryData.unlockedMods = [10];
			manager.initialize();
			mockPost.mockResolvedValue({ ok: false, error: 'nope' });

			const result = await manager.applyMod(1, 10, 0);

			expect(result).toBe(false);
			expect(manager.unlockedItems.get(1)?.appliedMods).toEqual([]);
		});

		it('returns false and makes no API call when the mod is not unlocked', async () => {
			mockItems[1] = makeItem(1);
			mockInventoryData.unlockedItems = [makeInventoryItem({ itemId: 1 })];
			manager.initialize();

			const result = await manager.applyMod(1, 10, 0);

			expect(result).toBe(false);
			expect(ApiRequest).not.toHaveBeenCalled();
		});

		it('returns false and makes no API call when the item is not unlocked', async () => {
			mockItemMods[10] = makeItemMod(10);
			mockInventoryData.unlockedMods = [10];
			manager.initialize();

			const result = await manager.applyMod(1, 10, 0);

			expect(result).toBe(false);
			expect(ApiRequest).not.toHaveBeenCalled();
		});

		it('returns false and does not log when the API returns an error', async () => {
			mockItems[1] = makeItem(1);
			mockItemMods[10] = makeItemMod(10);
			mockInventoryData.unlockedItems = [makeInventoryItem({ itemId: 1 })];
			mockInventoryData.unlockedMods = [10];
			manager.initialize();
			mockPost.mockResolvedValue({ ok: false, error: 'nope' });

			const result = await manager.applyMod(1, 10, 0);

			expect(result).toBe(false);
			expect(logMessage).not.toHaveBeenCalled();
		});
	});

	describe('removeMod', () => {
		it('posts to the API and logs on success', async () => {
			mockItems[1] = makeItem(1);
			mockInventoryData.unlockedItems = [makeInventoryItem({ itemId: 1 })];
			manager.initialize();

			const result = await manager.removeMod(1, 0);

			expect(result).toBe(true);
			expect(ApiRequest).toHaveBeenCalledWith('Player/RemoveMod');
			expect(mockPost).toHaveBeenCalledWith({ itemId: 1, itemModSlotId: 0 });
			expect(logMessage).toHaveBeenCalledWith(ELogType.ItemFound, 'Modifier removed.');
		});

		it('removes the mod from the authoritative item and equipmentStats on success', async () => {
			mockItems[1] = makeItem(1);
			mockItemMods[10] = makeItemMod(10);
			mockInventoryData.unlockedItems = [
				makeInventoryItem({
					itemId: 1,
					equipped: true,
					equipmentSlotId: EEquipmentSlot.WeaponSlot,
					appliedMods: [{ itemModId: 10, itemModSlotId: 0 }]
				})
			];
			mockInventoryData.unlockedMods = [10];
			manager.initialize();

			await manager.removeMod(1, 0);

			const item = manager.unlockedItems.get(1);
			expect(item?.appliedMods).toEqual([]);
			expect(item?.totalAttributes.getValue(EAttribute.Agility)).toBe(0);
			expect(manager.equipmentStats).toEqual([{ attributeId: EAttribute.Strength, amount: 5 }]);
		});

		it('returns false and makes no API call when the item is not unlocked', async () => {
			manager.initialize();

			const result = await manager.removeMod(1, 0);

			expect(result).toBe(false);
			expect(ApiRequest).not.toHaveBeenCalled();
		});

		it('returns false and does not log when the API returns an error', async () => {
			mockItems[1] = makeItem(1);
			mockInventoryData.unlockedItems = [makeInventoryItem({ itemId: 1 })];
			manager.initialize();
			mockPost.mockResolvedValue({ ok: false, error: 'nope' });

			const result = await manager.removeMod(1, 0);

			expect(result).toBe(false);
			expect(logMessage).not.toHaveBeenCalled();
		});

		it('leaves the applied mod in place when the API returns an error', async () => {
			mockItems[1] = makeItem(1);
			mockItemMods[10] = makeItemMod(10);
			mockInventoryData.unlockedItems = [
				makeInventoryItem({ itemId: 1, appliedMods: [{ itemModId: 10, itemModSlotId: 0 }] })
			];
			mockInventoryData.unlockedMods = [10];
			manager.initialize();
			mockPost.mockResolvedValue({ ok: false, error: 'nope' });

			const result = await manager.removeMod(1, 0);

			expect(result).toBe(false);
			expect(manager.unlockedItems.get(1)?.appliedMods.map((m) => m.id)).toEqual([10]);
		});
	});

	describe('setFavorite', () => {
		it('sets the flag and persists it over the socket', async () => {
			mockItems[1] = makeItem(1);
			mockInventoryData.unlockedItems = [makeInventoryItem({ itemId: 1 })];
			manager.initialize();

			const result = await manager.setFavorite(1, true);

			expect(result).toBe(true);
			expect(manager.unlockedItems.get(1)?.favorite).toBe(true);
			expect(mockSendSocketCommand).toHaveBeenCalledWith('SetItemFavorite', { itemId: 1, favorite: true });
		});

		it('returns false for an unknown item', async () => {
			manager.initialize();

			const result = await manager.setFavorite(99, true);

			expect(result).toBe(false);
			expect(mockSendSocketCommand).not.toHaveBeenCalled();
		});

		it('keeps the optimistic local flag when the socket send fails', async () => {
			mockItems[1] = makeItem(1);
			mockInventoryData.unlockedItems = [makeInventoryItem({ itemId: 1 })];
			manager.initialize();
			mockSendSocketCommand.mockRejectedValue(new Error('socket down'));

			const result = await manager.setFavorite(1, true);

			expect(result).toBe(true);
			expect(manager.unlockedItems.get(1)?.favorite).toBe(true);
		});
	});

	describe('addUnlockedItem', () => {
		it('adds a new item and logs', () => {
			mockItems[3] = makeItem(3);

			manager.addUnlockedItem({
				itemId: 3,
				equipped: false,
				favorite: false,
				appliedMods: []
			});

			expect(manager.unlockedItems.has(3)).toBe(true);
			expect(logMessage).toHaveBeenCalledWith(ELogType.ItemFound, expect.stringContaining('Item 3'));
		});

		it('is idempotent — an already-unlocked item is not overwritten or re-logged', () => {
			mockItems[3] = makeItem(3);
			mockInventoryData.unlockedItems = [
				{ itemId: 3, equipped: true, equipmentSlotId: 4, favorite: true, appliedMods: [] }
			];
			manager.initialize();
			vi.mocked(logMessage).mockClear();
			const existing = manager.unlockedItems.get(3);

			manager.addUnlockedItem({ itemId: 3, equipped: false, favorite: false, appliedMods: [] });

			// The existing item (with its equipped/favorite state) is preserved, not replaced.
			expect(manager.unlockedItems.get(3)).toBe(existing);
			expect(logMessage).not.toHaveBeenCalled();
		});
	});

	describe('addUnlockedMod', () => {
		it('adds a mod id and logs', () => {
			manager.addUnlockedMod(20);

			expect(manager.unlockedMods.has(20)).toBe(true);
			expect(logMessage).toHaveBeenCalledWith(ELogType.ItemFound, 'New modifier unlocked!');
		});

		it('reassigns the Set so reactive consumers re-derive', () => {
			const before = manager.unlockedMods;

			manager.addUnlockedMod(20);

			expect(manager.unlockedMods).not.toBe(before);
		});

		it('is idempotent — an already-unlocked mod is not re-added or re-logged', () => {
			mockInventoryData.unlockedMods = [20];
			manager.initialize();
			vi.mocked(logMessage).mockClear();
			const before = manager.unlockedMods;

			manager.addUnlockedMod(20);

			expect(manager.unlockedMods).toBe(before);
			expect(logMessage).not.toHaveBeenCalled();
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
