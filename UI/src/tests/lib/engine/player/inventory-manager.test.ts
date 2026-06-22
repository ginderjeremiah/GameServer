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

// Controllable stub for the network boundary every inventory mutation crosses. The socket transport
// always resolves an `IApiSocketResponse` (it never rejects) — a success resolves an object with no
// `error` field, a failure resolves one carrying `error` — so a test flips an endpoint to the
// error/rollback path by resolving `{ error }`.
const { mockSendSocketCommand } = vi.hoisted(() => {
	const mockSendSocketCommand = vi.fn();
	return { mockSendSocketCommand };
});

vi.mock('$lib/api', async (importOriginal) => {
	const actual = (await importOriginal()) as Record<string, unknown>;
	return {
		...actual,
		apiSocket: {
			sendSocketCommand: mockSendSocketCommand
		}
	};
});

vi.mock('$lib/engine/log', () => ({
	logMessage: vi.fn()
}));

import { InventoryManager, EEquipmentSlot, getEquipmentSlotForCategory } from '$lib/engine/player/inventory-manager';
import { logMessage } from '$lib/engine/log';
import type { IInventoryItem } from '$lib/api';

const makeItem = (id: number, category: EItemCategory = EItemCategory.Weapon, grantedSkillId?: number): IItem => ({
	id,
	name: `Item ${id}`,
	description: `Description ${id}`,
	itemCategoryId: category,
	rarityId: ERarity.Common,
	iconPath: `/icons/${id}.png`,
	grantedSkillId,
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

/** Drains the microtask queue so a queued mutation's optimistic apply lands before the assertion. */
const flush = () => new Promise((resolve) => setTimeout(resolve, 0));

describe('InventoryManager', () => {
	let manager: InventoryManager;

	beforeEach(() => {
		manager = new InventoryManager();
		vi.mocked(logMessage).mockClear();

		// Default the network boundary to success; error-path tests override per case. The socket
		// transport always resolves an `IApiSocketResponse` (never rejects), so a success resolves an
		// object with no `error` field rather than `undefined`.
		mockSendSocketCommand.mockReset().mockResolvedValue({});

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

		it('skips an unlocked item whose reference record is missing/retired without crashing', () => {
			// Item 1 resolves; item 2 has no reference record (retired or not yet downloaded) and must be
			// skipped — a single stale id can't take down the whole inventory load.
			mockItems[1] = makeItem(1);
			mockInventoryData.unlockedItems = [
				{ itemId: 1, equipped: false, appliedMods: [], favorite: false },
				{ itemId: 2, equipped: false, appliedMods: [], favorite: false }
			];

			expect(() => manager.initialize()).not.toThrow();

			expect(manager.unlockedItems.has(1)).toBe(true);
			expect(manager.unlockedItems.has(2)).toBe(false);
			expect(manager.unlockedItems.size).toBe(1);
			expect(logMessage).toHaveBeenCalledWith(ELogType.Debug, expect.stringContaining('2'));
		});

		it('does not place a skipped (unknown-id) equipped item into a slot', () => {
			// A retired equipped item must not be slotted — its record is gone, so there's nothing to place.
			mockInventoryData.unlockedItems = [
				{ itemId: 2, equipped: true, equipmentSlotId: EEquipmentSlot.WeaponSlot, appliedMods: [], favorite: false }
			];

			manager.initialize();

			expect(manager.equippedSlots[EEquipmentSlot.WeaponSlot]).toBeUndefined();
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

		it('memoizes the result, returning a stable reference until equipment changes', async () => {
			mockItems[1] = makeItem(1);
			mockInventoryData.unlockedItems = [makeInventoryItem({ itemId: 1 })];
			manager.initialize();

			const first = manager.equipmentStats;
			// Re-reading without a mutation returns the same memoized array rather than re-flattening.
			expect(manager.equipmentStats).toBe(first);

			await manager.equipItem(1, EEquipmentSlot.WeaponSlot);

			expect(manager.equipmentStats).not.toBe(first);
			expect(manager.equipmentStats).toEqual([{ attributeId: EAttribute.Strength, amount: 5 }]);
		});

		it('restores the memoized stats when an equip is rolled back', async () => {
			mockItems[1] = makeItem(1);
			mockInventoryData.unlockedItems = [makeInventoryItem({ itemId: 1 })];
			manager.initialize();
			mockSendSocketCommand.mockResolvedValue({ error: 'nope' });

			await manager.equipItem(1, EEquipmentSlot.WeaponSlot);

			// The optimistic equip is reverted on the failed persist, so the cache must follow it back.
			expect(manager.equipmentStats).toEqual([]);
		});
	});

	describe('grantedSkillIds', () => {
		it('returns empty when nothing is equipped', () => {
			manager.initialize();
			expect(manager.grantedSkillIds).toEqual([]);
		});

		it('gathers the equipped items granted skills in EEquipmentSlot order', () => {
			// Equip in reverse slot order (accessory first) to prove the gather follows slot index, not equip order.
			mockItems[1] = makeItem(1, EItemCategory.Accessory, 7);
			mockItems[2] = makeItem(2, EItemCategory.Helm, 3);
			mockInventoryData.unlockedItems = [
				makeInventoryItem({ itemId: 1, equipped: true, equipmentSlotId: EEquipmentSlot.AccessorySlot }),
				makeInventoryItem({ itemId: 2, equipped: true, equipmentSlotId: EEquipmentSlot.HelmSlot })
			];

			manager.initialize();

			// Helm (slot 0) before Accessory (slot 5).
			expect(manager.grantedSkillIds).toEqual([3, 7]);
		});

		it('skips equipped items that grant no skill', () => {
			mockItems[1] = makeItem(1, EItemCategory.Weapon); // no grant
			mockItems[2] = makeItem(2, EItemCategory.Helm, 4);
			mockInventoryData.unlockedItems = [
				makeInventoryItem({ itemId: 1, equipped: true, equipmentSlotId: EEquipmentSlot.WeaponSlot }),
				makeInventoryItem({ itemId: 2, equipped: true, equipmentSlotId: EEquipmentSlot.HelmSlot })
			];

			manager.initialize();

			expect(manager.grantedSkillIds).toEqual([4]);
		});

		it('reassigns a fresh list when equipment changes (so the battle reset re-derives)', async () => {
			mockItems[1] = makeItem(1, EItemCategory.Weapon, 9);
			mockInventoryData.unlockedItems = [makeInventoryItem({ itemId: 1 })];
			manager.initialize();

			const before = manager.grantedSkillIds;
			expect(before).toEqual([]);

			await manager.equipItem(1, EEquipmentSlot.WeaponSlot);

			expect(manager.grantedSkillIds).not.toBe(before);
			expect(manager.grantedSkillIds).toEqual([9]);
		});
	});

	describe('items (reactive published list)', () => {
		it('mirrors unlockedItemList and reflects an in-place field edit without rebuilding the array', async () => {
			mockItems[1] = makeItem(1);
			mockInventoryData.unlockedItems = [makeInventoryItem({ itemId: 1 })];
			manager.initialize();

			expect(manager.items).toBe(manager.unlockedItemList);
			expect(manager.items.map((i) => i.itemId)).toEqual([1]);

			const before = manager.items;
			await manager.equipItem(1, EEquipmentSlot.WeaponSlot);
			// A pure field edit mutates the item in place — statify keeps it reactive, so the array is not
			// rebuilt and the same reference now reflects the change.
			expect(manager.items).toBe(before);
			expect(manager.items.find((i) => i.itemId === 1)?.equipped).toBe(true);
		});

		it('rebuilds a fresh array when the item set grows (addUnlockedItem)', () => {
			mockItems[3] = makeItem(3);
			manager.initialize();
			const before = manager.items;

			manager.addUnlockedItem(makeInventoryItem({ itemId: 3 }));

			// A set change has no statified field to signal it, so publish rebuilds a fresh array reference.
			expect(manager.items).not.toBe(before);
			expect(manager.items.map((i) => i.itemId)).toEqual([3]);
		});
	});

	describe('equipItem', () => {
		it('equips an item into an empty slot and sends the socket command', async () => {
			mockItems[1] = makeItem(1);
			mockInventoryData.unlockedItems = [makeInventoryItem({ itemId: 1 })];
			manager.initialize();

			const result = await manager.equipItem(1, EEquipmentSlot.WeaponSlot);

			expect(result).toBe(true);
			expect(mockSendSocketCommand).toHaveBeenCalledWith('EquipItem', {
				itemId: 1,
				equipmentSlotId: EEquipmentSlot.WeaponSlot
			});
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

		it('returns false and makes no socket call for an unknown item', async () => {
			manager.initialize();

			const result = await manager.equipItem(99, EEquipmentSlot.WeaponSlot);

			expect(result).toBe(false);
			expect(mockSendSocketCommand).not.toHaveBeenCalled();
			expect(manager.equippedSlots[EEquipmentSlot.WeaponSlot]).toBeUndefined();
		});

		it('leaves state unchanged when the socket returns an error', async () => {
			mockItems[1] = makeItem(1);
			mockInventoryData.unlockedItems = [makeInventoryItem({ itemId: 1 })];
			manager.initialize();
			mockSendSocketCommand.mockResolvedValue({ error: 'nope' });

			const result = await manager.equipItem(1, EEquipmentSlot.WeaponSlot);

			expect(result).toBe(false);
			expect(manager.equippedSlots[EEquipmentSlot.WeaponSlot]).toBeUndefined();
			expect(manager.unlockedItems.get(1)?.equipped).toBe(false);
		});

		it('restores both the displaced item and its slot when the persist fails', async () => {
			mockItems[1] = makeItem(1);
			mockItems[2] = makeItem(2);
			mockInventoryData.unlockedItems = [
				makeInventoryItem({ itemId: 1, equipped: true, equipmentSlotId: EEquipmentSlot.WeaponSlot }),
				makeInventoryItem({ itemId: 2 })
			];
			manager.initialize();
			mockSendSocketCommand.mockResolvedValue({ error: 'nope' });

			const result = await manager.equipItem(2, EEquipmentSlot.WeaponSlot);

			// The displaced incumbent (item 1) is fully restored to its slot, and the new item (item 2)
			// is rolled back to unequipped — the single snapshot covers every object the equip touched.
			expect(result).toBe(false);
			expect(manager.equippedSlots[EEquipmentSlot.WeaponSlot]?.itemId).toBe(1);
			expect(manager.unlockedItems.get(1)?.equipped).toBe(true);
			expect(manager.unlockedItems.get(1)?.equipmentSlotId).toBe(EEquipmentSlot.WeaponSlot);
			expect(manager.unlockedItems.get(2)?.equipped).toBe(false);
			expect(manager.unlockedItems.get(2)?.equipmentSlotId).toBeUndefined();
		});

		it('applies the change optimistically before the persist resolves, then rolls back on error', async () => {
			mockItems[1] = makeItem(1);
			mockInventoryData.unlockedItems = [makeInventoryItem({ itemId: 1 })];
			manager.initialize();

			let resolveSend: (value: { error?: string }) => void = () => {};
			mockSendSocketCommand.mockReturnValue(new Promise((resolve) => (resolveSend = resolve)));

			const pending = manager.equipItem(1, EEquipmentSlot.WeaponSlot);
			// The mutation queues onto the serialization chain, so the optimistic apply lands on the next
			// microtask rather than synchronously; flush it, then confirm it applied with the persist still in flight.
			await flush();
			expect(manager.equippedSlots[EEquipmentSlot.WeaponSlot]?.itemId).toBe(1);
			expect(manager.unlockedItems.get(1)?.equipped).toBe(true);

			resolveSend({ error: 'nope' });
			await pending;

			expect(manager.equippedSlots[EEquipmentSlot.WeaponSlot]).toBeUndefined();
			expect(manager.unlockedItems.get(1)?.equipped).toBe(false);
		});
	});

	describe('overlapping mutations (serialization)', () => {
		beforeEach(() => {
			// Items 1 & 2 are weapons that contest the same slot; item 3 (helm) exercises an independent
			// slot; mod 10 lets a test overlap an equip with an apply-mod across mutation types.
			mockItems[1] = makeItem(1, EItemCategory.Weapon);
			mockItems[2] = makeItem(2, EItemCategory.Weapon);
			mockItems[3] = makeItem(3, EItemCategory.Helm);
			mockItemMods[10] = makeItemMod(10);
			mockInventoryData.unlockedItems = [
				makeInventoryItem({ itemId: 1 }),
				makeInventoryItem({ itemId: 2 }),
				makeInventoryItem({ itemId: 3 })
			];
			mockInventoryData.unlockedMods = [10];
			manager.initialize();
		});

		it('queues the second mutation until the first has settled before it snapshots and persists', async () => {
			let resolveFirst: (value: { error?: string }) => void = () => {};
			mockSendSocketCommand.mockReturnValueOnce(new Promise((resolve) => (resolveFirst = resolve)));

			const first = manager.equipItem(1, EEquipmentSlot.WeaponSlot);
			const second = manager.equipItem(3, EEquipmentSlot.HelmSlot);

			// The first op has applied optimistically and is awaiting its persist; the second is still
			// queued behind it — it has neither applied nor reached the network.
			await flush();
			expect(manager.equippedSlots[EEquipmentSlot.WeaponSlot]?.itemId).toBe(1);
			expect(manager.equippedSlots[EEquipmentSlot.HelmSlot]).toBeUndefined();
			expect(mockSendSocketCommand).toHaveBeenCalledTimes(1);

			resolveFirst({});
			await Promise.all([first, second]);

			expect(mockSendSocketCommand).toHaveBeenCalledTimes(2);
			expect(manager.equippedSlots[EEquipmentSlot.HelmSlot]?.itemId).toBe(3);
		});

		it('keeps local state consistent with the server when the first of two same-slot equips fails', async () => {
			// First equip fails, second succeeds. Without serialization the first op's rollback baseline
			// (captured before the second applied) would restore an empty slot, discarding the second
			// change while the server has it applied — the desync this issue fixes.
			mockSendSocketCommand.mockReset();
			mockSendSocketCommand.mockResolvedValueOnce({ error: 'nope' });
			mockSendSocketCommand.mockResolvedValue({});

			const [firstResult, secondResult] = await Promise.all([
				manager.equipItem(1, EEquipmentSlot.WeaponSlot),
				manager.equipItem(2, EEquipmentSlot.WeaponSlot)
			]);

			expect(firstResult).toBe(false);
			expect(secondResult).toBe(true);
			// Server ends with the second item equipped; local state must match.
			expect(manager.equippedSlots[EEquipmentSlot.WeaponSlot]?.itemId).toBe(2);
			expect(manager.unlockedItems.get(2)?.equipped).toBe(true);
			expect(manager.unlockedItems.get(2)?.equipmentSlotId).toBe(EEquipmentSlot.WeaponSlot);
			expect(manager.unlockedItems.get(1)?.equipped).toBe(false);
			expect(manager.unlockedItems.get(1)?.equipmentSlotId).toBeUndefined();
		});

		it('leaves the slot empty when both overlapping same-slot equips fail (no stale-baseline resurrection)', async () => {
			// Both persists fail. Without serialization the second op's rollback restores a baseline taken
			// after the first op's optimistic apply, resurrecting the first item even though its own
			// persist already rolled it back — leaving an item shown as equipped that the server never saved.
			mockSendSocketCommand.mockReset();
			mockSendSocketCommand.mockResolvedValue({ error: 'nope' });

			const [firstResult, secondResult] = await Promise.all([
				manager.equipItem(1, EEquipmentSlot.WeaponSlot),
				manager.equipItem(2, EEquipmentSlot.WeaponSlot)
			]);

			expect(firstResult).toBe(false);
			expect(secondResult).toBe(false);
			expect(manager.equippedSlots[EEquipmentSlot.WeaponSlot]).toBeUndefined();
			expect(manager.unlockedItems.get(1)?.equipped).toBe(false);
			expect(manager.unlockedItems.get(1)?.equipmentSlotId).toBeUndefined();
			expect(manager.unlockedItems.get(2)?.equipped).toBe(false);
			expect(manager.unlockedItems.get(2)?.equipmentSlotId).toBeUndefined();
		});

		it('keeps the queue alive when a mutation rejects, so the next mutation still runs', async () => {
			// The socket transport never rejects in practice (it resolves failures with an `error` field),
			// but a thrown operation must not wedge the chain: the rejection is isolated and the queued
			// follow-up still snapshots and persists.
			mockSendSocketCommand.mockReset();
			mockSendSocketCommand.mockRejectedValueOnce(new Error('boom'));
			mockSendSocketCommand.mockResolvedValue({});

			const first = manager.equipItem(1, EEquipmentSlot.WeaponSlot);
			const second = manager.equipItem(3, EEquipmentSlot.HelmSlot);

			await expect(first).rejects.toThrow('boom');
			await expect(second).resolves.toBe(true);
			expect(manager.equippedSlots[EEquipmentSlot.HelmSlot]?.itemId).toBe(3);
		});

		it('serializes across mutation types — applyMod waits for a pending equip to settle', async () => {
			// All four mutations route through the same chain, so an apply-mod queued behind an in-flight
			// equip must not snapshot or persist until the equip settles.
			let resolveEquip: (value: { error?: string }) => void = () => {};
			mockSendSocketCommand.mockReturnValueOnce(new Promise((resolve) => (resolveEquip = resolve)));

			const equip = manager.equipItem(1, EEquipmentSlot.WeaponSlot);
			const mod = manager.applyMod(1, 10, 0);

			await flush();
			expect(manager.unlockedItems.get(1)?.equipped).toBe(true);
			expect(manager.unlockedItems.get(1)?.appliedMods).toEqual([]);
			expect(mockSendSocketCommand).toHaveBeenCalledTimes(1);

			resolveEquip({});
			await Promise.all([equip, mod]);

			expect(mockSendSocketCommand).toHaveBeenCalledTimes(2);
			expect(manager.unlockedItems.get(1)?.equipped).toBe(true);
			expect(manager.unlockedItems.get(1)?.appliedMods.map((m) => m.id)).toEqual([10]);
		});

		it('serializes setFavorite behind a pending equip so its optimistic write cannot interleave', async () => {
			// A favorite toggle must wait out an in-flight item op rather than writing its flag between
			// that op's snapshot and rollback — so it routes through the same chain as the other mutations.
			let resolveEquip: (value: { error?: string }) => void = () => {};
			mockSendSocketCommand.mockReturnValueOnce(new Promise((resolve) => (resolveEquip = resolve)));

			const equip = manager.equipItem(1, EEquipmentSlot.WeaponSlot);
			const fav = manager.setFavorite(1, true);

			await flush();
			// The equip applied optimistically and awaits its persist; the favorite toggle is still queued
			// behind it — neither its flag write nor its socket call has happened yet.
			expect(manager.unlockedItems.get(1)?.equipped).toBe(true);
			expect(manager.unlockedItems.get(1)?.favorite).toBe(false);
			expect(mockSendSocketCommand).toHaveBeenCalledTimes(1);

			resolveEquip({});
			await Promise.all([equip, fav]);

			expect(mockSendSocketCommand).toHaveBeenCalledTimes(2);
			expect(mockSendSocketCommand).toHaveBeenLastCalledWith('SetItemFavorite', { itemId: 1, favorite: true });
			expect(manager.unlockedItems.get(1)?.favorite).toBe(true);
		});
	});

	describe('unequipItem', () => {
		it('unequips the item in a slot and sends the socket command', async () => {
			mockItems[1] = makeItem(1);
			mockInventoryData.unlockedItems = [
				makeInventoryItem({ itemId: 1, equipped: true, equipmentSlotId: EEquipmentSlot.WeaponSlot })
			];
			manager.initialize();
			const item = manager.unlockedItems.get(1);

			const result = await manager.unequipItem(EEquipmentSlot.WeaponSlot);

			expect(result).toBe(true);
			expect(mockSendSocketCommand).toHaveBeenCalledWith('UnequipItem', {
				itemId: 1,
				equipmentSlotId: EEquipmentSlot.WeaponSlot
			});
			expect(manager.equippedSlots[EEquipmentSlot.WeaponSlot]).toBeUndefined();
			expect(item?.equipped).toBe(false);
			expect(item?.equipmentSlotId).toBeUndefined();
		});

		it('returns false and makes no socket call for an empty slot', async () => {
			manager.initialize();

			const result = await manager.unequipItem(EEquipmentSlot.WeaponSlot);

			expect(result).toBe(false);
			expect(mockSendSocketCommand).not.toHaveBeenCalled();
		});

		it('leaves the item equipped when the socket returns an error', async () => {
			mockItems[1] = makeItem(1);
			mockInventoryData.unlockedItems = [
				makeInventoryItem({ itemId: 1, equipped: true, equipmentSlotId: EEquipmentSlot.WeaponSlot })
			];
			manager.initialize();
			mockSendSocketCommand.mockResolvedValue({ error: 'nope' });

			const result = await manager.unequipItem(EEquipmentSlot.WeaponSlot);

			expect(result).toBe(false);
			expect(manager.equippedSlots[EEquipmentSlot.WeaponSlot]?.itemId).toBe(1);
		});
	});

	describe('applyMod', () => {
		it('sends the socket command and logs on success', async () => {
			mockItems[1] = makeItem(1);
			mockItemMods[10] = makeItemMod(10);
			mockInventoryData.unlockedItems = [makeInventoryItem({ itemId: 1 })];
			mockInventoryData.unlockedMods = [10];
			manager.initialize();

			const result = await manager.applyMod(1, 10, 0);

			expect(result).toBe(true);
			expect(mockSendSocketCommand).toHaveBeenCalledWith('ApplyMod', {
				itemId: 1,
				itemModId: 10,
				itemModSlotId: 0
			});
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

		it('leaves the authoritative item unchanged when the socket returns an error', async () => {
			mockItems[1] = makeItem(1);
			mockItemMods[10] = makeItemMod(10);
			mockInventoryData.unlockedItems = [makeInventoryItem({ itemId: 1 })];
			mockInventoryData.unlockedMods = [10];
			manager.initialize();
			mockSendSocketCommand.mockResolvedValue({ error: 'nope' });

			const result = await manager.applyMod(1, 10, 0);

			expect(result).toBe(false);
			expect(manager.unlockedItems.get(1)?.appliedMods).toEqual([]);
		});

		it('returns false and makes no socket call when the mod is not unlocked', async () => {
			mockItems[1] = makeItem(1);
			mockInventoryData.unlockedItems = [makeInventoryItem({ itemId: 1 })];
			manager.initialize();

			const result = await manager.applyMod(1, 10, 0);

			expect(result).toBe(false);
			expect(mockSendSocketCommand).not.toHaveBeenCalled();
		});

		it('returns false and makes no socket call when the item is not unlocked', async () => {
			mockItemMods[10] = makeItemMod(10);
			mockInventoryData.unlockedMods = [10];
			manager.initialize();

			const result = await manager.applyMod(1, 10, 0);

			expect(result).toBe(false);
			expect(mockSendSocketCommand).not.toHaveBeenCalled();
		});

		it('returns false and makes no socket call when the mod has no reference record', async () => {
			// The mod is unlocked but its reference record is missing/retired (no mockItemMods entry), so
			// newItemMod resolves to undefined — bail rather than apply a mod that can't be resolved.
			mockItems[1] = makeItem(1);
			mockInventoryData.unlockedItems = [makeInventoryItem({ itemId: 1 })];
			mockInventoryData.unlockedMods = [10];
			manager.initialize();

			const result = await manager.applyMod(1, 10, 0);

			expect(result).toBe(false);
			expect(mockSendSocketCommand).not.toHaveBeenCalled();
			expect(manager.unlockedItems.get(1)?.appliedMods).toEqual([]);
		});

		it('returns false and does not log when the socket returns an error', async () => {
			mockItems[1] = makeItem(1);
			mockItemMods[10] = makeItemMod(10);
			mockInventoryData.unlockedItems = [makeInventoryItem({ itemId: 1 })];
			mockInventoryData.unlockedMods = [10];
			manager.initialize();
			mockSendSocketCommand.mockResolvedValue({ error: 'nope' });

			const result = await manager.applyMod(1, 10, 0);

			expect(result).toBe(false);
			expect(logMessage).not.toHaveBeenCalled();
		});
	});

	describe('removeMod', () => {
		it('sends the socket command and logs on success', async () => {
			mockItems[1] = makeItem(1);
			mockInventoryData.unlockedItems = [makeInventoryItem({ itemId: 1 })];
			manager.initialize();

			const result = await manager.removeMod(1, 0);

			expect(result).toBe(true);
			expect(mockSendSocketCommand).toHaveBeenCalledWith('RemoveMod', {
				itemId: 1,
				itemModSlotId: 0
			});
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

		it('returns false and makes no socket call when the item is not unlocked', async () => {
			manager.initialize();

			const result = await manager.removeMod(1, 0);

			expect(result).toBe(false);
			expect(mockSendSocketCommand).not.toHaveBeenCalled();
		});

		it('returns false and does not log when the socket returns an error', async () => {
			mockItems[1] = makeItem(1);
			mockInventoryData.unlockedItems = [makeInventoryItem({ itemId: 1 })];
			manager.initialize();
			mockSendSocketCommand.mockResolvedValue({ error: 'nope' });

			const result = await manager.removeMod(1, 0);

			expect(result).toBe(false);
			expect(logMessage).not.toHaveBeenCalled();
		});

		it('leaves the applied mod in place when the socket returns an error', async () => {
			mockItems[1] = makeItem(1);
			mockItemMods[10] = makeItemMod(10);
			mockInventoryData.unlockedItems = [
				makeInventoryItem({ itemId: 1, appliedMods: [{ itemModId: 10, itemModSlotId: 0 }] })
			];
			mockInventoryData.unlockedMods = [10];
			manager.initialize();
			mockSendSocketCommand.mockResolvedValue({ error: 'nope' });

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

		it('keeps the optimistic local flag and logs when the socket reports an error', async () => {
			mockItems[1] = makeItem(1);
			mockInventoryData.unlockedItems = [makeInventoryItem({ itemId: 1 })];
			manager.initialize();
			// The transport never rejects — it resolves every failure with an `error` field — so the
			// failure must be observed via `response.error` and logged rather than swallowed by a catch.
			mockSendSocketCommand.mockResolvedValue({ error: 'socket down' });

			const result = await manager.setFavorite(1, true);

			expect(result).toBe(true);
			expect(manager.unlockedItems.get(1)?.favorite).toBe(true);
			expect(logMessage).toHaveBeenCalledWith(ELogType.Debug, expect.stringContaining('socket down'));
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

		it('ignores a reward for an item whose reference record is missing/retired without crashing', () => {
			// No reference record for id 5 ⇒ the grant degrades gracefully instead of crashing the reward path.
			manager.initialize();

			expect(() => manager.addUnlockedItem(makeInventoryItem({ itemId: 5 }))).not.toThrow();

			expect(manager.unlockedItems.has(5)).toBe(false);
			expect(logMessage).toHaveBeenCalledWith(ELogType.Debug, expect.stringContaining('5'));
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
