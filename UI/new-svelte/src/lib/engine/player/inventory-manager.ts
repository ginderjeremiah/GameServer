import { IInventoryItem, IBattlerAttribute, ELogType, EItemCategory, ApiRequest } from '$lib/api';
import { playerManager } from '$lib/engine';
import { Item, newItem } from '$lib/battle';
import { logMessage } from '$lib/engine/log';

//Manually putting this here until codegen gets updated to load this
export enum EEquipmentSlot {
	HelmSlot = 0,
	ChestSlot = 1,
	LegSlot = 2,
	BootSlot = 3,
	WeaponSlot = 4,
	AccessorySlot = 5
}

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
		if (!item) return false;

		const req = new ApiRequest('Player/EquipItem');
		const response = await req.post({ itemId, equipmentSlotId: slotId });
		if (response.error) return false;

		// Unequip from any current slot
		for (let i = 0; i < this.equippedSlots.length; i++) {
			if (this.equippedSlots[i]?.itemId === itemId) {
				const old = this.equippedSlots[i]!;
				old.equipped = false;
				old.equipmentSlotId = undefined;
				this.equippedSlots[i] = undefined;
			}
		}

		// Unequip whatever is in the target slot
		if (this.equippedSlots[slotId]) {
			const displaced = this.equippedSlots[slotId]!;
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
		if (!item) return false;

		const req = new ApiRequest('Player/UnequipItem');
		const response = await req.post({ itemId: item.itemId, equipmentSlotId: slotId });
		if (response.error) return false;

		item.equipped = false;
		item.equipmentSlotId = undefined;
		this.equippedSlots[slotId] = undefined;

		return true;
	}

	public async applyMod(itemId: number, itemModId: number, itemModSlotId: number) {
		if (!this.unlockedMods.has(itemModId)) return false;
		if (!this.unlockedItems.has(itemId)) return false;

		const req = new ApiRequest('Player/ApplyMod');
		const response = await req.post({ itemId, itemModId, itemModSlotId });
		if (response.error) return false;

		// Re-fetch player data to get updated item state
		// (or update locally — for now just log success)
		logMessage(ELogType.Inventory, 'Modifier applied.');

		return true;
	}

	public async removeMod(itemId: number, itemModSlotId: number) {
		if (!this.unlockedItems.has(itemId)) return false;

		const req = new ApiRequest('Player/RemoveMod');
		const response = await req.post({ itemId, itemModSlotId });
		if (response.error) return false;

		logMessage(ELogType.Inventory, 'Modifier removed.');

		return true;
	}

	/** Called when the player unlocks a new item from a challenge reward. */
	public addUnlockedItem(invItem: IInventoryItem) {
		const item = newItem(invItem);
		this.unlockedItems.set(invItem.itemId, item);
		logMessage(ELogType.Inventory, `Unlocked: ${item.name}!`);
	}

	/** Called when the player unlocks a new mod from a challenge reward. */
	public addUnlockedMod(modId: number) {
		this.unlockedMods.add(modId);
		logMessage(ELogType.Inventory, 'New modifier unlocked!');
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
