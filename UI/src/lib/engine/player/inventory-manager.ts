import {
	IInventoryItem,
	IBattlerAttribute,
	ELogType,
	EItemCategory,
	EEquipmentSlot,
	EDamageType,
	apiSocket
} from '$lib/api';
import { PUNCH_SKILL_ID } from '$lib/api/types/game-constants';
import { playerManager } from '$lib/engine';
import { Item, newItem, newItemMod } from '$lib/battle';
import { logMessage } from '$lib/engine/log';
import { SerializedQueue } from '$lib/common';
import { staticData } from '$stores';

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
	 * edits (equip, favorite, mods) propagate to consumers on their own without a rebuild. This relies on
	 * `Item`/`ItemMod` being classes (not plain object literals): `statify` gives a class instance its
	 * own reactive field accessors, defined once and shared by every container that references it —
	 * `unlockedItems`, `items`, and `equippedSlots` all read/write the same object. A plain-object item
	 * would instead pick up Svelte's own deep-proxy per container, giving the "same" item a different
	 * proxied identity in the Map than in this array — mutations through one would be invisible to
	 * consumers reading the other (#1957).
	 */
	public items: Item[] = [];

	/**
	 * Memoised combined attributes of all equipped items + their applied mods. Rebuilt only on the
	 * equip/unequip/mod mutations below (and their rollbacks), so the per-spawn battle reset and the
	 * inventory/skills `$derived` chains read a stable array instead of re-flattening every equipped
	 * item + mod on each access (#811).
	 */
	private equipmentStatsCache: IBattlerAttribute[] = [];

	/**
	 * Memoised ids of the skills the equipped items grant, gathered in EEquipmentSlot order (the slot index
	 * order of {@link equippedSlots}). Rebuilt alongside {@link equipmentStats} on every equip/unequip/mod
	 * mutation, so the battle reset reads a stable, slot-ordered list to append to the player's loadout
	 * (de-duplicated against the selected skills in {@link Battler}).
	 */
	private grantedSkillIdsCache: number[] = [];

	/** Serializes the optimistic mutations so rollback baselines never interleave (see {@link serialize}). */
	private queue = new SerializedQueue();

	/**
	 * Bumped on every {@link initialize}. `initialize()` runs outside the {@link queue} (a mid-session
	 * resync can't wait behind an in-flight mutation's persist), so a mutation that started optimistically
	 * applying before a resync — then has its persist fail afterward — must not roll back: its snapshot
	 * predates the resync's fresh item instances, and restoring it would splice orphaned pre-resync objects
	 * back into live state (#1808). Each mutation captures the generation at snapshot time and checks it
	 * via {@link rollbackIfCurrent} before invoking its rollback.
	 */
	private generation = 0;

	public initialize() {
		this.generation++;
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

	/** Ids of the skills the equipped items grant, in EEquipmentSlot order (memoised — recomputed only by
	 *  {@link refreshEquipmentStats} on an equip/unequip/mod change). The battle build appends these to the
	 *  player's loadout, de-duplicated against the selected skills. Includes the virtual-fists punch
	 *  ({@link PUNCH_SKILL_ID}) when no weapon is equipped — the bare-hands signature (#1342). */
	public get grantedSkillIds(): number[] {
		return this.grantedSkillIdsCache;
	}

	/** The type the weapon-match gate keys on (#1342): the equipped weapon's `weaponType`, or `Unarmed` for
	 *  the virtual fists when the weapon slot is empty. Always defined for the player battler (a player is
	 *  always either armed or bare-handed); an enemy battler passes no type and is ungated. A weapon is
	 *  authored with a weapon-leaf type, so the `?? Unarmed` is a defensive fallback. */
	public get equippedWeaponType(): EDamageType {
		return this.equippedSlots[EEquipmentSlot.WeaponSlot]?.weaponType ?? EDamageType.Unarmed;
	}

	/** The id of the skill the player ripostes with on a parry (#1457): the equipped weapon's signature, or
	 *  the virtual fists' punch bare-handed — the same resolution rule the granted-signature append uses,
	 *  mirroring the backend `BattleSnapshot.ToBattler`. `undefined` when the id resolves to no authored
	 *  skill (an unseeded punch), so the battler carries no counter and a parry negates without a riposte. */
	public get counterSkillId(): number | undefined {
		const id = this.equippedSlots[EEquipmentSlot.WeaponSlot]?.grantedSkillId ?? PUNCH_SKILL_ID;
		return staticData.skills?.[id] != null ? id : undefined;
	}

	/** Rebuilds the memoised {@link equipmentStats} and {@link grantedSkillIds} from the currently equipped
	 *  items + their mods. Called after every mutation that can change them (and after a rollback restores
	 *  the prior state). Reassigns both caches so the battle reset's by-reference change detection fires. */
	private refreshEquipmentStats() {
		const stats: IBattlerAttribute[] = [];
		const grantedSkillIds: number[] = [];
		for (const item of this.equippedSlots) {
			if (item) {
				stats.push(...item.attributes);
				for (const mod of item.appliedMods) {
					stats.push(...mod.attributes);
				}
				if (item.grantedSkillId != null) {
					grantedSkillIds.push(item.grantedSkillId);
				}
			}
		}
		// Virtual-fists punch (#1342): with no weapon equipped, the weapon slot's signature is the configured
		// punch skill, appended after the item grants (mirroring the backend BattleSnapshot.GetBattleSkillIds).
		// Guarded on the skill resolving — like the backend's nullable lookup — so an unauthored punch is
		// skipped rather than fielding a phantom slot. A real Unarmed weapon (brass knuckles) fields its own
		// signature instead and gets no punch.
		if (this.equippedSlots[EEquipmentSlot.WeaponSlot] == null && staticData.skills?.[PUNCH_SKILL_ID] != null) {
			grantedSkillIds.push(PUNCH_SKILL_ID);
		}
		this.equipmentStatsCache = stats;
		this.grantedSkillIdsCache = grantedSkillIds;
	}

	public equipItem(itemId: number, slotId: EEquipmentSlot): Promise<boolean> {
		return this.serialize(async () => {
			const item = this.unlockedItems.get(itemId);
			if (!item) {
				return false;
			}

			// Apply optimistically (instant UI), then persist; a failed persist rolls the change back so
			// the authoritative state can never diverge from what was actually saved.
			const startGeneration = this.generation;
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
				this.rollbackIfCurrent(startGeneration, rollback, () => this.refreshEquipmentStats());
				return false;
			}

			// The response carries the authoritative post-equip rating (spike #1526 Decision 7), so
			// adopt it absolutely rather than leaving the displayed Power stale until the next full
			// player-state refresh (#1616).
			if (response.data) {
				playerManager.playerRating = response.data.playerRating;
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

			const startGeneration = this.generation;
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
				this.rollbackIfCurrent(startGeneration, rollback, () => this.refreshEquipmentStats());
				return false;
			}

			// The response carries the authoritative post-unequip rating (spike #1526 Decision 7), so
			// adopt it absolutely rather than leaving the displayed Power stale until the next full
			// player-state refresh (#1616).
			if (response.data) {
				playerManager.playerRating = response.data.playerRating;
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

			const startGeneration = this.generation;
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
				this.rollbackIfCurrent(startGeneration, rollback, () => this.refreshEquipmentStatsIfEquipped(item));
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

			const startGeneration = this.generation;
			const rollback = snapshotFields(item, 'appliedMods', 'totalAttributes');
			item.appliedMods = item.appliedMods.filter((m) => m.itemModSlotId !== itemModSlotId);
			this.refreshItemAttributes(item);
			this.refreshEquipmentStatsIfEquipped(item);

			const response = await apiSocket.sendSocketCommand('RemoveMod', {
				itemId,
				itemModSlotId
			});
			if (response.error) {
				this.rollbackIfCurrent(startGeneration, rollback, () => this.refreshEquipmentStatsIfEquipped(item));
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
	 * local state diverged from the server. The shared {@link SerializedQueue} queues the actions instead,
	 * which is better UX than dropping the second one.
	 */
	private serialize<T>(operation: () => Promise<T>): Promise<T> {
		return this.queue.run(operation);
	}

	/**
	 * Invokes `rollback` only if no {@link initialize} resync has happened since `startGeneration` was
	 * captured. `initialize()` runs outside the {@link queue}, so a mid-flight mutation's persist can fail
	 * after a resync has already rebuilt `unlockedItems`/`equippedSlots` with fresh objects; rolling back at
	 * that point would restore the stale pre-resync snapshot — reintroducing orphaned item references the
	 * resync just replaced (#1808). When the generation has moved on, the resync's own state is left as-is.
	 */
	private rollbackIfCurrent(startGeneration: number, rollback: RestoreSnapshot, after: () => void) {
		if (this.generation !== startGeneration) {
			return;
		}
		rollback();
		after();
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
		item.totalAttributes = item.recomputeTotalAttributes();
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
