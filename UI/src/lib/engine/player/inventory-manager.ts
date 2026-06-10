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

export class InventoryManager {
	/** All items the player has unlocked, keyed by itemId. */
	public unlockedItems: Map<number, Item> = new Map();

	/** IDs of all modifiers the player has unlocked. */
	public unlockedMods: Set<number> = new Set();

	/** The 6 equipment slots — index matches EEquipmentSlot. */
	public equippedSlots: (Item | undefined)[] = new Array(6).fill(undefined);

	/** Currently selected item (for mod customization panel). */
	public selectedItemId: number | undefined;

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
	}

	public get unlockedItemList(): Item[] {
		return [...this.unlockedItems.values()];
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

	public get selectedItem(): Item | undefined {
		return this.selectedItemId != null ? this.unlockedItems.get(this.selectedItemId) : undefined;
	}

	public selectItem(itemId: number) {
		this.selectedItemId = itemId;
	}

	public async equipItem(itemId: number, slotId: EEquipmentSlot) {
		const item = this.unlockedItems.get(itemId);
		if (!item) {
			return false;
		}

		const req = new ApiRequest('Player/EquipItem');
		const response = await req.post({ itemId, equipmentSlotId: slotId });
		if (response.error) {
			return false;
		}

		// Unequip from any current slot
		for (let i = 0; i < this.equippedSlots.length; i++) {
			const old = this.equippedSlots[i];
			if (old?.itemId === itemId) {
				old.equipped = false;
				old.equipmentSlotId = undefined;
				this.equippedSlots[i] = undefined;
			}
		}

		// Unequip whatever is in the target slot
		const displaced = this.equippedSlots[slotId];
		if (displaced) {
			displaced.equipped = false;
			displaced.equipmentSlotId = undefined;
		}

		// Equip the new item
		item.equipped = true;
		item.equipmentSlotId = slotId;
		this.equippedSlots[slotId] = item;

		return true;
	}

	public async unequipItem(slotId: EEquipmentSlot) {
		const item = this.equippedSlots[slotId];
		if (!item) {
			return false;
		}

		const req = new ApiRequest('Player/UnequipItem');
		const response = await req.post({ itemId: item.itemId, equipmentSlotId: slotId });
		if (response.error) {
			return false;
		}

		item.equipped = false;
		item.equipmentSlotId = undefined;
		this.equippedSlots[slotId] = undefined;

		return true;
	}

	public async applyMod(itemId: number, itemModId: number, itemModSlotId: number) {
		const item = this.unlockedItems.get(itemId);
		if (!this.unlockedMods.has(itemModId) || !item) {
			return false;
		}

		const req = new ApiRequest('Player/ApplyMod');
		const response = await req.post({ itemId, itemModId, itemModSlotId });
		if (response.error) {
			return false;
		}

		// Mirror the change onto the authoritative item (the equip/favorite precedent) so
		// equipmentStats (battle) and any view re-seed reflect it without a page reload.
		item.appliedMods = [
			...item.appliedMods.filter((m) => m.itemModSlotId !== itemModSlotId),
			newItemMod({ itemModId, itemModSlotId })
		];
		this.refreshItemAttributes(item);
		logMessage(ELogType.ItemFound, 'Modifier applied.');

		return true;
	}

	public async removeMod(itemId: number, itemModSlotId: number) {
		const item = this.unlockedItems.get(itemId);
		if (!item) {
			return false;
		}

		const req = new ApiRequest('Player/RemoveMod');
		const response = await req.post({ itemId, itemModSlotId });
		if (response.error) {
			return false;
		}

		item.appliedMods = item.appliedMods.filter((m) => m.itemModSlotId !== itemModSlotId);
		this.refreshItemAttributes(item);
		logMessage(ELogType.ItemFound, 'Modifier removed.');

		return true;
	}

	/** Rebuilds an item's cached totalAttributes after its applied mods change. */
	private refreshItemAttributes(item: Item) {
		const allAttributes = [...item.attributes, ...item.appliedMods.flatMap((mod) => mod.attributes)];
		item.totalAttributes = new BattleAttributes(allAttributes, false);
	}

	/**
	 * Toggles whether an item is favorited and persists it via a websocket
	 * command. The local flag is updated optimistically; a failed send keeps
	 * the local state (it re-syncs on the next toggle or on reload).
	 */
	public async setFavorite(itemId: number, favorite: boolean) {
		const item = this.unlockedItems.get(itemId);
		if (!item) {
			return false;
		}

		item.favorite = favorite;
		try {
			await apiSocket.sendSocketCommand('SetItemFavorite', { itemId, favorite });
		} catch {
			// Keep the optimistic local state; it re-syncs on the next toggle/reload.
		}
		return true;
	}

	/** Called when the player unlocks a new item from a challenge reward. */
	public addUnlockedItem(invItem: IInventoryItem) {
		const item = newItem(invItem);
		this.unlockedItems.set(invItem.itemId, item);
		logMessage(ELogType.ItemFound, `Unlocked: ${item.name}!`);
	}

	/** Called when the player unlocks a new mod from a challenge reward. */
	public addUnlockedMod(modId: number) {
		this.unlockedMods.add(modId);
		logMessage(ELogType.ItemFound, 'New modifier unlocked!');
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
