import { IInventoryItem, IBattlerAttribute, ELogType, EItemCategory, EEquipmentSlot, apiSocket } from '$lib/api';
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
	 * Reactive published view of `unlockedItems`. The manager is the single owner of the item objects
	 * and the only place they are mutated. `statify` makes each item's fields (and this array) reactive
	 * in place, so the array is rebuilt (`publish`) only when the item *set* changes; in-place field
	 * edits (equip, favorite, mods) propagate to consumers on their own without a rebuild.
	 */
	public items: Item[] = [];

	/**
	 * Memoised combined attributes of all equipped items + their applied mods. Rebuilt only on the
	 * equip/unequip/mod mutations below (and their rollbacks), so the per-spawn battle reset and the
	 * inventory/skills `$derived` chains read a stable array instead of re-flattening every equipped
	 * item + mod on each access (#811).
	 */
	private equipmentStatsCache: IBattlerAttribute[] = [];

	/** Tail of the optimistic-mutation queue; each mutation chains off it so rollback baselines never interleave. */
	private lastOperation: Promise<unknown> = Promise.resolve();

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
			// Skip an item whose reference record is missing/retired rather than crashing inventory load.
			if (!item) {
				logMessage(ELogType.Debug, `Skipped unlocked item with unknown id ${invItem.itemId}.`);
				continue;
			}
			this.unlockedItems.set(invItem.itemId, item);

			// Place equipped items into their equipment slots
			if (invItem.equipped && invItem.equipmentSlotId != null) {
				this.equippedSlots[invItem.equipmentSlotId] = item;
			}
		}

		this.publish();
		this.refreshEquipmentStats();
	}

	public get unlockedItemList(): Item[] {
		return this.items;
	}

	/** Combined attributes from all equipped items and their applied mods (memoised — recomputed only
	 *  by {@link refreshEquipmentStats} on an equip/unequip/mod change, so reads are O(1)). */
	public get equipmentStats(): IBattlerAttribute[] {
		return this.equipmentStatsCache;
	}

	/** Rebuilds the memoised {@link equipmentStats} from the currently equipped items + their mods.
	 *  Called after every mutation that can change them (and after a rollback restores the prior state). */
	private refreshEquipmentStats() {
		const stats: IBattlerAttribute[] = [];
		for (const item of this.equippedSlots) {
			if (item) {
				stats.push(...item.attributes);
				for (const mod of item.appliedMods) {
					stats.push(...mod.attributes);
				}
			}
		}
		this.equipmentStatsCache = stats;
	}

	public equipItem(itemId: number, slotId: EEquipmentSlot): Promise<boolean> {
		return this.serialize(async () => {
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

			const response = await apiSocket.sendSocketCommand('EquipItem', {
				itemId,
				equipmentSlotId: slotId
			});
			if (response.error) {
				rollback();
				this.refreshEquipmentStats();
				return false;
			}

			return true;
		});
	}

	public unequipItem(slotId: EEquipmentSlot): Promise<boolean> {
		return this.serialize(async () => {
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
			this.refreshEquipmentStats();

			const response = await apiSocket.sendSocketCommand('UnequipItem', {
				itemId: item.itemId,
				equipmentSlotId: slotId
			});
			if (response.error) {
				rollback();
				this.refreshEquipmentStats();
				return false;
			}

			return true;
		});
	}

	public applyMod(itemId: number, itemModId: number, itemModSlotId: number): Promise<boolean> {
		return this.serialize(async () => {
			const item = this.unlockedItems.get(itemId);
			const mod = newItemMod({ itemModId, itemModSlotId });
			// Bail if the item, the mod's unlock, or the mod's reference record (missing/retired) is absent.
			if (!this.unlockedMods.has(itemModId) || !item || !mod) {
				return false;
			}

			const rollback = snapshotFields(item, 'appliedMods', 'totalAttributes');
			item.appliedMods = [...item.appliedMods.filter((m) => m.itemModSlotId !== itemModSlotId), mod];
			this.refreshItemAttributes(item);
			this.refreshEquipmentStatsIfEquipped(item);

			const response = await apiSocket.sendSocketCommand('ApplyMod', {
				itemId,
				itemModId,
				itemModSlotId
			});
			if (response.error) {
				rollback();
				this.refreshEquipmentStatsIfEquipped(item);
				return false;
			}

			logMessage(ELogType.ItemFound, 'Modifier applied.');
			return true;
		});
	}

	public removeMod(itemId: number, itemModSlotId: number): Promise<boolean> {
		return this.serialize(async () => {
			const item = this.unlockedItems.get(itemId);
			if (!item) {
				return false;
			}

			const rollback = snapshotFields(item, 'appliedMods', 'totalAttributes');
			item.appliedMods = item.appliedMods.filter((m) => m.itemModSlotId !== itemModSlotId);
			this.refreshItemAttributes(item);
			this.refreshEquipmentStatsIfEquipped(item);

			const response = await apiSocket.sendSocketCommand('RemoveMod', {
				itemId,
				itemModSlotId
			});
			if (response.error) {
				rollback();
				this.refreshEquipmentStatsIfEquipped(item);
				return false;
			}

			logMessage(ELogType.ItemFound, 'Modifier removed.');
			return true;
		});
	}

	/**
	 * Toggles whether an item is favorited and persists it via a websocket command. The local flag is
	 * updated optimistically; a failed send keeps the local state (it re-syncs on the next toggle or
	 * on reload) rather than rolling back, since a favourite flag is low-stakes. The transport resolves
	 * every failure with `response.error` (it never rejects), so the failure is observed and logged.
	 *
	 * Routed through {@link serialize} like the other optimistic mutations so its optimistic write can't
	 * interleave with an in-flight item op's snapshot/rollback baseline — a favorite toggle resolving
	 * between another op's snapshot and its rollback would otherwise race the rebuild. The no-rollback
	 * policy lives inside the serialized closure (the toggle stays applied on a failed persist).
	 */
	public setFavorite(itemId: number, favorite: boolean): Promise<boolean> {
		return this.serialize(async () => {
			const item = this.unlockedItems.get(itemId);
			if (!item) {
				return false;
			}

			item.favorite = favorite;
			const response = await apiSocket.sendSocketCommand('SetItemFavorite', { itemId, favorite });
			if (response.error) {
				logMessage(ELogType.Debug, 'There was an error setting the item favorite: ' + response.error);
			}
			return true;
		});
	}

	/** Called when the player unlocks a new item from a challenge reward. */
	public addUnlockedItem(invItem: IInventoryItem) {
		if (this.unlockedItems.has(invItem.itemId)) {
			return;
		}
		const item = newItem(invItem);
		// Ignore a reward referencing a missing/retired item rather than crashing the grant path.
		if (!item) {
			logMessage(ELogType.Debug, `Skipped unlocking item with unknown id ${invItem.itemId}.`);
			return;
		}
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

	/**
	 * Serializes the optimistic mutations (equip/unequip/applyMod/removeMod/setFavorite) so each one captures its
	 * rollback baseline only after the previous mutation has fully settled — persist and any rollback
	 * included. Overlapping callers (a double-click equip, dragging a second item while the first
	 * persist is still in flight) would otherwise interleave baselines: one operation's rollback could
	 * restore a snapshot taken before another's optimistic change, silently discarding it and leaving
	 * local state diverged from the server. Chaining onto a single promise — the same "collapse
	 * concurrency" spirit as auth.ts's single-flight refresh — queues the actions instead, which is
	 * better UX than dropping the second one.
	 */
	private serialize<T>(operation: () => Promise<T>): Promise<T> {
		const result = this.lastOperation.then(operation, operation);
		// The queue tracks settlement only — swallowing a failure here keeps one operation's rejection
		// from breaking the chain or surfacing as an unhandled rejection on the shared promise.
		this.lastOperation = result.catch(() => undefined);
		return result;
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
		this.refreshEquipmentStats();
	}

	/** Rebuilds an item's cached totalAttributes after its applied mods change. */
	private refreshItemAttributes(item: Item) {
		const allAttributes = [...item.attributes, ...item.appliedMods.flatMap((mod) => mod.attributes)];
		item.totalAttributes = new BattleAttributes(allAttributes, false);
	}

	/** Refreshes the memoised equipmentStats only when the changed item is equipped — an unequipped
	 *  item's mods don't contribute, so its mod edits must not churn the cache (or its consumers). */
	private refreshEquipmentStatsIfEquipped(item: Item) {
		if (item.equipped) {
			this.refreshEquipmentStats();
		}
	}

	/**
	 * Rebuilds the published `items` array from `unlockedItems`. Only needed when the item *set* changes
	 * (initialize, addUnlockedItem): `statify` keeps in-place field edits reactive on their own, so pure
	 * field mutations (equip, favorite, mods) propagate without a rebuild.
	 */
	private publish() {
		this.items = [...this.unlockedItems.values()];
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
