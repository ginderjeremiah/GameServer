import { EDamageType, EItemCategory, ERarity, IBattlerAttribute, IInventoryItem, IItem, IItemModSlot } from '$lib/api';
import { ItemMod, newItemMod } from './item-mod';
import { BattleAttributes } from './battle-attributes';
import { staticData } from '$stores';

// A class (not a plain object literal) so `statify` recognizes it via its own per-field reactivity
// (see `statify.svelte.ts`) instead of falling through to Svelte's native deep-proxy, which would give
// the same underlying item a different identity depending on which reactive array reads it. That
// divergence is what let `InventoryManager.unlockedItems` (a Map, never itself deep-proxied) hold a
// raw item while the UI read a separately-proxied copy of the same object — mutations through the Map
// were invisible to the UI (#1957). Keeping `Item`/`ItemMod` as classes preserves one shared identity
// across every container (the Map, `items`, `equippedSlots`) that references the same item.
export class Item implements IItem {
	id: number;
	name: string;
	description: string;
	itemCategoryId: EItemCategory;
	rarityId: ERarity;
	iconPath: string;
	attributes: IBattlerAttribute[];
	modSlots: IItemModSlot[];
	tags: number[];
	grantedSkillId?: number;
	weaponType?: EDamageType;
	requiredProficiencyId?: number;
	requiredProficiencyLevel: number;
	designerNotes: string;
	retiredAt?: string;

	itemId: number;
	equipped: boolean;
	equipmentSlotId?: number;
	favorite: boolean;
	appliedMods: ItemMod[];
	totalAttributes: BattleAttributes;

	constructor(itemData: IItem, invItem: IInventoryItem) {
		this.id = itemData.id;
		this.name = itemData.name;
		this.description = itemData.description;
		this.itemCategoryId = itemData.itemCategoryId;
		this.rarityId = itemData.rarityId;
		this.iconPath = itemData.iconPath;
		this.attributes = itemData.attributes;
		this.modSlots = itemData.modSlots;
		this.tags = itemData.tags;
		this.grantedSkillId = itemData.grantedSkillId;
		this.weaponType = itemData.weaponType;
		this.requiredProficiencyId = itemData.requiredProficiencyId;
		this.requiredProficiencyLevel = itemData.requiredProficiencyLevel;
		this.designerNotes = itemData.designerNotes;
		this.retiredAt = itemData.retiredAt;

		this.itemId = invItem.itemId;
		this.equipped = invItem.equipped;
		this.equipmentSlotId = invItem.equipmentSlotId;
		this.favorite = invItem.favorite ?? false;
		// Drop any applied mod whose own definition is missing/retired so one stale mod can't crash the item.
		this.appliedMods = invItem.appliedMods.map((am) => newItemMod(am)).filter((mod): mod is ItemMod => mod != null);
		this.totalAttributes = this.recomputeTotalAttributes();
	}

	/** Rebuilds the cached totalAttributes from the item's base attributes plus its applied mods. */
	recomputeTotalAttributes(): BattleAttributes {
		return new BattleAttributes([...this.attributes, ...this.appliedMods.flatMap((mod) => mod.attributes)], false);
	}
}

export const newItem = (invItem: IInventoryItem): Item | undefined => {
	// Resolve the item's static definition by id; a missing/retired id yields no record, so degrade
	// gracefully (mirroring resolveUnlockReward) rather than spreading `undefined` and crashing.
	const itemData = staticData.items?.[invItem.itemId];
	if (!itemData) {
		return undefined;
	}
	return new Item(itemData, invItem);
};
