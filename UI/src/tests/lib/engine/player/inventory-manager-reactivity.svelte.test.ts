import { describe, it, expect, vi, beforeEach } from 'vitest';
import { flushSync } from 'svelte';
import { EAttribute, EItemCategory, EItemModType, ERarity } from '$lib/api';
import type { IInventoryData, IInventoryItem, IItem, IItemMod } from '$lib/api';
import { statify } from '$lib/common';

const mockInventoryData: IInventoryData = {
	unlockedItems: [],
	unlockedMods: []
};

vi.mock('$lib/engine', () => ({
	playerManager: {
		get inventoryData() {
			return mockInventoryData;
		},
		playerRating: 0
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
		},
		get skills() {
			return [];
		}
	}
}));

const { mockSendSocketCommand } = vi.hoisted(() => {
	const mockSendSocketCommand = vi.fn();
	return { mockSendSocketCommand };
});

vi.mock('$lib/api', async (importOriginal) => {
	const actual = (await importOriginal()) as Record<string, unknown>;
	return {
		...actual,
		apiSocket: { sendSocketCommand: mockSendSocketCommand }
	};
});

vi.mock('$lib/engine/log', () => ({ logMessage: vi.fn() }));

import { InventoryManager, EEquipmentSlot } from '$lib/engine/player/inventory-manager';

const makeItem = (id: number): IItem => ({
	id,
	name: `Item ${id}`,
	description: `Description ${id}`,
	designerNotes: '',
	itemCategoryId: EItemCategory.Weapon,
	rarityId: ERarity.Common,
	iconPath: `/icons/${id}.png`,
	requiredProficiencyLevel: 0,
	attributes: [{ attributeId: EAttribute.Strength, amount: 5 }],
	modSlots: [{ id: 0, itemId: id, itemModSlotTypeId: EItemModType.Component }],
	tags: []
});

const makeItemMod = (id: number): IItemMod => ({
	id,
	name: `Mod ${id}`,
	description: `Mod description ${id}`,
	designerNotes: '',
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

// The real app wraps the manager in `statify` (`$lib/engine/engine.ts`), and every mutation path
// resolves its target item via `unlockedItems.get(...)`. These guard that such a mutation is observed
// by a `$derived` reading the same item through the reactive `items` array — the two access paths must
// resolve to the same statified object, or the UI silently goes stale (#1957).
describe('InventoryManager reactivity under statify (#1957)', () => {
	let manager: InventoryManager;

	beforeEach(() => {
		manager = statify(new InventoryManager());
		mockItems.length = 0;
		mockItemMods.length = 0;
		mockInventoryData.unlockedItems = [];
		mockInventoryData.unlockedMods = [];
		mockSendSocketCommand.mockReset().mockResolvedValue({});
	});

	it('observes a favorite toggle through the items array', async () => {
		mockItems[1] = makeItem(1);
		mockInventoryData.unlockedItems = [makeInventoryItem({ itemId: 1 })];
		manager.initialize();

		let favorite: boolean | undefined;
		const cleanup = $effect.root(() => {
			const derived = $derived(manager.items[0]?.favorite);
			$effect(() => {
				favorite = derived;
			});
		});
		flushSync();
		expect(favorite).toBe(false);

		await manager.setFavorite(1, true);
		flushSync();
		expect(favorite).toBe(true);

		cleanup();
	});

	it('observes an equip/unequip toggle through the items array', async () => {
		mockItems[1] = makeItem(1);
		mockInventoryData.unlockedItems = [makeInventoryItem({ itemId: 1 })];
		manager.initialize();

		let equipped: boolean | undefined;
		const cleanup = $effect.root(() => {
			const derived = $derived(manager.items[0]?.equipped);
			$effect(() => {
				equipped = derived;
			});
		});
		flushSync();
		expect(equipped).toBe(false);

		await manager.equipItem(1, EEquipmentSlot.WeaponSlot);
		flushSync();
		expect(equipped).toBe(true);

		await manager.unequipItem(EEquipmentSlot.WeaponSlot);
		flushSync();
		expect(equipped).toBe(false);

		cleanup();
	});

	it('observes an applied mod change through the items array', async () => {
		mockItems[1] = makeItem(1);
		mockItemMods[10] = makeItemMod(10);
		mockInventoryData.unlockedItems = [makeInventoryItem({ itemId: 1 })];
		mockInventoryData.unlockedMods = [10];
		manager.initialize();

		let modCount: number | undefined;
		const cleanup = $effect.root(() => {
			const derived = $derived(manager.items[0]?.appliedMods.length);
			$effect(() => {
				modCount = derived;
			});
		});
		flushSync();
		expect(modCount).toBe(0);

		await manager.applyMod(1, 10, 0);
		flushSync();
		expect(modCount).toBe(1);

		await manager.removeMod(1, 0);
		flushSync();
		expect(modCount).toBe(0);

		cleanup();
	});

	it('observes unlockedItems/unlockedMods changes through a resync (initialize called again)', () => {
		mockItems[1] = makeItem(1);
		mockItemMods[10] = makeItemMod(10);
		mockInventoryData.unlockedItems = [];
		mockInventoryData.unlockedMods = [];
		manager.initialize();

		let itemCount: number | undefined;
		let modUnlocked: boolean | undefined;
		const cleanup = $effect.root(() => {
			const derivedItemCount = $derived(manager.unlockedItems.size);
			const derivedModUnlocked = $derived(manager.unlockedMods.has(10));
			$effect(() => {
				itemCount = derivedItemCount;
				modUnlocked = derivedModUnlocked;
			});
		});
		flushSync();
		expect(itemCount).toBe(0);
		expect(modUnlocked).toBe(false);

		// Simulate a resync delivering an item/mod that weren't present at the last initialize.
		mockInventoryData.unlockedItems = [makeInventoryItem({ itemId: 1 })];
		mockInventoryData.unlockedMods = [10];
		manager.initialize();
		flushSync();
		expect(itemCount).toBe(1);
		expect(modUnlocked).toBe(true);

		cleanup();
	});
});
