import {
	IInventoryItem,
	IBattlerAttribute,
	ELogType,
	EItemCategory,
	EEquipmentSlot,
	ApiRequest,
	apiSocket
} from '$lib/api';
import { playerManager } from '$lib/engine';
import { BattleAttributes, Item, newItem, newItemMod } from '$lib/battle';
import { logMessage } from '$lib/engine/log';

// Re-exported from the generated client so the established `$lib/engine` import sites keep resolving
// it here while the single source of truth is the codegen'd enum.
export { EEquipmentSlot };

type RestoreSnapshot = () => void;

/**
 * Captures the current values of the named fields on `target`, returning a closure that restores
 * them — the single snapshot/restore primitive behind every optimistic mutation's rollback. Captured
 * fields must be reassigned (not mutated in place) for the snapshot to stay a valid baseline.
 */
const snapshotFields = <T extends object, K extends keyof T>(target: T, ...keys: K[]): RestoreSnapshot => {
	const saved = keys.map((key) => ({ key, value: target[key] }));
	return () => {
		for (const { key, value } of saved) {
			target[key] = value;
		}
	};
};

/** Combines several field snapshots into a single restore closure. */
const combineSnapshots = (...restores: RestoreSnapshot[]): RestoreSnapshot => {
	return () => {
		for (const restore of restores) {
			restore();
		}
	};
};

export class InventoryManager {
	/** All items the player has unlocked, keyed by itemId. */
	public unlockedItems: Map<number, Item> = new Map();

	/** IDs of all modifiers the player has unlocked. */
	public unlockedMods: Set<number> = new Set();

	/** The 6 equipment slots — index matches EEquipmentSlot. */
	public equippedSlots: (Item | undefined)[] = new Array(6).fill(undefined);

	/**
	 * Reactive published view of `unlockedItems`. The manager is the single owner of the item
	 * objects and the only place they are mutated; this snapshot is republished (`publish`) on every
	 * change so reactive consumers (the inventory screen) re-derive without keeping their own copies.
	 */
	public items: Item[] = [];

	public initialize() {
		this.unlockedItems.clear();
		this.unlockedMods.clear();
		this.equippedSlots = new Array(6).fill(undefined);

		const data = playerManager.inventoryData;

		// Load unlocked mods
		for (const modId of data.unlockedMods) {
			this.unlockedMods.add(modId);
		}

		// Load unlocked items
		for (const invItem of data.unlockedItems) {
			const item = newItem(invItem);
			this.unlockedItems.set(invItem.itemId, item);

			// Place equipped items into their equipment slots
			if (invItem.equipped && invItem.equipmentSlotId != null) {
				this.equippedSlots[invItem.equipmentSlotId] = item;
			}
		}

		this.publish();
	}

	public get unlockedItemList(): Item[] {
		return this.items;
	}

	/** Computes combined attributes from all equipped items and their applied mods. */
	public get equipmentStats(): IBattlerAttribute[] {
		const stats: IBattlerAttribute[] = [];
		for (const item of this.equippedSlots) {
			if (item) {
				stats.push(...item.attributes);
				for (const mod of item.appliedMods) {
					stats.push(...mod.attributes);
				}
			}
		}
		return stats;
	}

	public async equipItem(itemId: number, slotId: EEquipmentSlot) {
		const item = this.unlockedItems.get(itemId);
		if (!item) {
			return false;
		}

		// Apply optimistically (instant UI), then persist; a failed persist rolls the change back so
		// the authoritative state can never diverge from what was actually saved.
		const affected = [item, this.equippedSlots[slotId]].filter((it): it is Item => it != null);
		const rollback = combineSnapshots(
			snapshotFields(this, 'equippedSlots'),
			...affected.map((it) => snapshotFields(it, 'equipped', 'equipmentSlotId'))
		);
		this.applyEquip(item, slotId);
		this.publish();

		const req = new ApiRequest('Player/EquipItem');
		const response = await req.post({ itemId, equipmentSlotId: slotId });
		if (!response.ok) {
			rollback();
			this.publish();
			return false;
		}

		return true;
	}

	public async unequipItem(slotId: EEquipmentSlot) {
		const item = this.equippedSlots[slotId];
		if (!item) {
			return false;
		}

		const rollback = combineSnapshots(
			snapshotFields(this, 'equippedSlots'),
			snapshotFields(item, 'equipped', 'equipmentSlotId')
		);
		item.equipped = false;
		item.equipmentSlotId = undefined;
		const slots = [...this.equippedSlots];
		slots[slotId] = undefined;
		this.equippedSlots = slots;
		this.publish();

		const req = new ApiRequest('Player/UnequipItem');
		const response = await req.post({ itemId: item.itemId, equipmentSlotId: slotId });
		if (!response.ok) {
			rollback();
			this.publish();
			return false;
		}

		return true;
	}

	public async applyMod(itemId: number, itemModId: number, itemModSlotId: number) {
		const item = this.unlockedItems.get(itemId);
		if (!this.unlockedMods.has(itemModId) || !item) {
			return false;
		}

		const rollback = snapshotFields(item, 'appliedMods', 'totalAttributes');
		item.appliedMods = [
			...item.appliedMods.filter((m) => m.itemModSlotId !== itemModSlotId),
			newItemMod({ itemModId, itemModSlotId })
		];
		this.refreshItemAttributes(item);
		this.publish();

		const req = new ApiRequest('Player/ApplyMod');
		const response = await req.post({ itemId, itemModId, itemModSlotId });
		if (!response.ok) {
			rollback();
			this.publish();
			return false;
		}

		logMessage(ELogType.ItemFound, 'Modifier applied.');
		return true;
	}

	public async removeMod(itemId: number, itemModSlotId: number) {
		const item = this.unlockedItems.get(itemId);
		if (!item) {
			return false;
		}

		const rollback = snapshotFields(item, 'appliedMods', 'totalAttributes');
		item.appliedMods = item.appliedMods.filter((m) => m.itemModSlotId !== itemModSlotId);
		this.refreshItemAttributes(item);
		this.publish();

		const req = new ApiRequest('Player/RemoveMod');
		const response = await req.post({ itemId, itemModSlotId });
		if (!response.ok) {
			rollback();
			this.publish();
			return false;
		}

		logMessage(ELogType.ItemFound, 'Modifier removed.');
		return true;
	}

	/**
	 * Toggles whether an item is favorited and persists it via a websocket command. The local flag is
	 * updated optimistically; a failed send keeps the local state (it re-syncs on the next toggle or
	 * on reload) rather than rolling back, since a favourite flag is low-stakes.
	 */
	public async setFavorite(itemId: number, favorite: boolean) {
		const item = this.unlockedItems.get(itemId);
		if (!item) {
			return false;
		}

		item.favorite = favorite;
		this.publish();
		try {
			await apiSocket.sendSocketCommand('SetItemFavorite', { itemId, favorite });
		} catch {
			// Keep the optimistic local state; it re-syncs on the next toggle/reload.
		}
		return true;
	}

	/** Called when the player unlocks a new item from a challenge reward. */
	public addUnlockedItem(invItem: IInventoryItem) {
		if (this.unlockedItems.has(invItem.itemId)) {
			return;
		}
		const item = newItem(invItem);
		this.unlockedItems.set(invItem.itemId, item);
		this.publish();
		logMessage(ELogType.ItemFound, `Unlocked: ${item.name}!`);
	}

	/** Called when the player unlocks a new mod from a challenge reward. */
	public addUnlockedMod(modId: number) {
		if (this.unlockedMods.has(modId)) {
			return;
		}
		// Reassign (not mutate) the Set so statify/$state tracks the change and reactive consumers —
		// e.g. an open mod picker's `compatibleMods` — re-derive immediately, mirroring addUnlockedSkill.
		this.unlockedMods = new Set(this.unlockedMods).add(modId);
		logMessage(ELogType.ItemFound, 'New modifier unlocked!');
	}

	/** The equip mutation, reassigning a fresh slot array so a captured snapshot stays a valid baseline. */
	private applyEquip(item: Item, slotId: EEquipmentSlot) {
		const slots = [...this.equippedSlots];

		// Unequip from any current slot
		for (let i = 0; i < slots.length; i++) {
			const old = slots[i];
			if (old?.itemId === item.itemId) {
				old.equipped = false;
				old.equipmentSlotId = undefined;
				slots[i] = undefined;
			}
		}

		// Unequip whatever is in the target slot
		const displaced = slots[slotId];
		if (displaced) {
			displaced.equipped = false;
			displaced.equipmentSlotId = undefined;
		}

		// Equip the new item
		item.equipped = true;
		item.equipmentSlotId = slotId;
		slots[slotId] = item;

		this.equippedSlots = slots;
	}

	/** Rebuilds an item's cached totalAttributes after its applied mods change. */
	private refreshItemAttributes(item: Item) {
		const allAttributes = [...item.attributes, ...item.appliedMods.flatMap((mod) => mod.attributes)];
		item.totalAttributes = new BattleAttributes(allAttributes, false);
	}

	/** Republishes the reactive item snapshot so consumers re-derive after a mutation. */
	private publish() {
		this.items = [...this.unlockedItems.values()];
		this.equippedSlots = [...this.equippedSlots];
	}
}

export const getEquipmentSlotForCategory = (category: EItemCategory): EEquipmentSlot => {
	switch (category) {
		case EItemCategory.Helm:
			return EEquipmentSlot.HelmSlot;
		case EItemCategory.Chest:
			return EEquipmentSlot.ChestSlot;
		case EItemCategory.Leg:
			return EEquipmentSlot.LegSlot;
		case EItemCategory.Boot:
			return EEquipmentSlot.BootSlot;
		case EItemCategory.Weapon:
			return EEquipmentSlot.WeaponSlot;
		case EItemCategory.Accessory:
		default:
			return EEquipmentSlot.AccessorySlot;
	}
};
