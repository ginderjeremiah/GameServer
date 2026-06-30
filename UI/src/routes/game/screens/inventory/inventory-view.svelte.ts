import { EDamageType, EItemCategory, type ISkill } from '$lib/api';
import { EEquipmentSlot, getEquipmentSlotForCategory, inventoryManager, playerManager } from '$lib/engine';
import { BattleAttributes, newlyDormantSkills, type AttributeEntry, type Item, type ItemMod } from '$lib/battle';
import { meetsItemProficiencyRequirement } from '$lib/common';
import { confirmModal, playerProficiencies, staticData, toastError } from '$stores';

/* ─── Static config ─────────────────────────────────────────────────────
   Equipment slots are declared here (extensible): adding a second weapon
   slot or splitting accessories is a one-line edit and the layout re-flows. */
export interface EquipSlotDef {
	id: EEquipmentSlot;
	label: string;
	category: EItemCategory;
	group: 'armor' | 'arms';
}

export const EQUIP_SLOTS: EquipSlotDef[] = [
	{ id: EEquipmentSlot.HelmSlot, label: 'Helm', category: EItemCategory.Helm, group: 'armor' },
	{ id: EEquipmentSlot.ChestSlot, label: 'Chest', category: EItemCategory.Chest, group: 'armor' },
	{ id: EEquipmentSlot.LegSlot, label: 'Legs', category: EItemCategory.Leg, group: 'armor' },
	{ id: EEquipmentSlot.BootSlot, label: 'Boots', category: EItemCategory.Boot, group: 'armor' },
	{ id: EEquipmentSlot.WeaponSlot, label: 'Weapon', category: EItemCategory.Weapon, group: 'arms' },
	{ id: EEquipmentSlot.AccessorySlot, label: 'Accessory', category: EItemCategory.Accessory, group: 'arms' }
];

export const EQUIP_GROUPS: { key: 'armor' | 'arms'; label: string }[] = [
	{ key: 'armor', label: 'Armor' },
	{ key: 'arms', label: 'Arms' }
];

export const FILTER_CATEGORIES: EItemCategory[] = [
	EItemCategory.Helm,
	EItemCategory.Chest,
	EItemCategory.Leg,
	EItemCategory.Boot,
	EItemCategory.Weapon,
	EItemCategory.Accessory
];

export type SortKey = 'name' | 'category';
export const SORTS: Record<SortKey, { label: string; cmp: (a: Item, b: Item) => number }> = {
	name: { label: 'Name', cmp: (a, b) => a.name.localeCompare(b.name) },
	category: { label: 'Category', cmp: (a, b) => a.itemCategoryId - b.itemCategoryId || a.name.localeCompare(b.name) }
};

/** The inventory's named-stat row, sharing the projection shape produced by `BattleAttributes`. */
export type StatEntry = AttributeEntry;

/* ─── Reactive view-model ────────────────────────────────────────────────
   Holds only the UI state for the inventory screen (sort/filter/selection).
   The item data and every mutation live on the authoritative `inventoryManager`;
   the view reads through its reactive `items`/`equippedSlots` and delegates
   actions, so there is a single source of truth and a single mutation path. */
export class InventoryView {
	sort = $state<SortKey>('category');
	filterCat = $state<EItemCategory | null>(null);
	favOnly = $state(false);
	selectedId = $state<number | null>(null);
	dragItemId = $state<number | null>(null);
	page = $state(0);

	/** The authoritative unlocked items, read reactively through the manager. */
	get items(): Item[] {
		return inventoryManager.unlockedItemList;
	}

	readonly equippedBySlot = $derived.by(() => {
		const map: Partial<Record<EEquipmentSlot, Item>> = {};
		inventoryManager.equippedSlots.forEach((item, slot) => {
			if (item) {
				map[slot as EEquipmentSlot] = item;
			}
		});
		return map;
	});

	// Resolve selection/drag through the manager's itemId-keyed Map (its own O(1) access convention)
	// rather than a linear scan of the list; `items` stays the source for list/count derivations.
	// NOTE: unlockedItems is a plain (non-reactive) Map, so these re-resolve only on id changes, not on
	// set changes — fine while items are add-only; if a removal path lands, clear the ids on removal.
	readonly selected = $derived(
		this.selectedId != null ? (inventoryManager.unlockedItems.get(this.selectedId) ?? null) : null
	);

	readonly dragItem = $derived(
		this.dragItemId != null ? (inventoryManager.unlockedItems.get(this.dragItemId) ?? null) : null
	);

	readonly counts = $derived.by(() => {
		const c: { all: number; fav: number; cats: Record<number, number> } = {
			all: this.items.length,
			fav: this.items.filter((i) => i.favorite).length,
			cats: {}
		};
		for (const it of this.items) {
			c.cats[it.itemCategoryId] = (c.cats[it.itemCategoryId] ?? 0) + 1;
		}
		return c;
	});

	readonly visible = $derived.by(() => {
		let list = this.items.slice();
		if (this.favOnly) {
			list = list.filter((i) => i.favorite);
		}
		if (this.filterCat != null) {
			list = list.filter((i) => i.itemCategoryId === this.filterCat);
		}
		return list.sort(SORTS[this.sort].cmp);
	});

	// Display totals reuse the manager's single equipmentStats derivation rather than re-flattening
	// equipped items, projecting it to a named stat list for the UI.
	readonly equippedTotals = $derived.by<StatEntry[]>(() =>
		new BattleAttributes(inventoryManager.equipmentStats, false).getAttributeMap()
	);

	readonly slotsFilled = $derived(inventoryManager.equippedSlots.filter((s) => s != null).length);

	/* ── actions ─────────────────────────────────────────────────────── */

	// Filter/sort changes snap back to the first page here — where the user intent originates —
	// so page-reset never lives in an effect that writes tracked state. `pageClamped` in the grid
	// remains the out-of-range safety net (e.g. when the visible list shrinks for other reasons).
	setSort(sort: SortKey) {
		this.sort = sort;
		this.page = 0;
	}

	setFilterCat(filterCat: EItemCategory | null) {
		this.filterCat = filterCat;
		this.page = 0;
	}

	setFavOnly(favOnly: boolean) {
		this.favOnly = favOnly;
		this.page = 0;
	}

	/** The "All" affordance clears both the category and favorites filters in one action. */
	showAll() {
		this.filterCat = null;
		this.favOnly = false;
		this.page = 0;
	}

	select(itemId: number | null) {
		this.selectedId = itemId;
	}

	/** Whether the player meets an item's proficiency gate (always true for ungated gear). Mirrors the
	    backend equip anti-cheat; used to disable the equip affordance and block the action. */
	canEquip(item: Item): boolean {
		return meetsItemProficiencyRequirement(item, (id) => playerProficiencies.levelOf(id));
	}

	/** Selected skills that would go dormant if the weapon slot's type became `nextWeaponType` — fielded under
	 *  the currently-equipped weapon, dimmed after the swap. Derived from the same weapon-match gate the battle
	 *  and Skills screen use (#1342), so it can't claim a skill dims that the battle would still field. */
	weaponSwapDormantSkills(nextWeaponType: EDamageType): ISkill[] {
		const skills = staticData.skills ?? [];
		const selected = playerManager.selectedSkills.map((id) => skills[id]).filter((s): s is ISkill => s != null);
		return newlyDormantSkills(selected, inventoryManager.equippedWeaponType, nextWeaponType);
	}

	/** Prompts the player before a weapon equip that would dim part of the saved loadout, naming the affected
	 *  `dormant` skills. Returns true to proceed (the player accepted), false to abort. The loadout itself is
	 *  never edited — the skills simply go dormant until a matching weapon is re-equipped. */
	private confirmWeaponSwap(item: Item, dormant: ISkill[]): Promise<boolean> {
		const single = dormant.length === 1;
		const names = dormant.map((s) => s.name).join(', ');
		return confirmModal({
			title: 'Some skills will go dormant',
			body:
				`Equipping ${item.name} will leave ${single ? 'this skill' : `these ${dormant.length} skills`} ` +
				`dormant until you re-equip a matching weapon: ${names}. Your loadout stays saved.`,
			confirmLabel: 'Equip anyway',
			cancelLabel: 'Keep current weapon'
		});
	}

	// Inventory mutations apply optimistically and roll back on a persist error; await the manager's
	// boolean and surface a toast on failure so the silent revert is reported, not swallowed.
	async equip(itemId: number, slotId: EEquipmentSlot) {
		// UI-side proficiency gate: refuse before the optimistic apply so a gated item never flickers
		// equipped (the backend would reject it anyway). The drawer also disables the affordance.
		const item = inventoryManager.unlockedItems.get(itemId);
		if (item && !this.canEquip(item)) {
			toastError('You have not met the proficiency requirement to equip this item.');
			return;
		}
		// Weapon-match warning (#1342): equipping a weapon can dim off-weapon selected skills. Warn (and let
		// the player back out) before the optimistic apply so a silently-dormant loadout isn't a surprise.
		// The dormant set is computed synchronously, so the no-conflict path never awaits a modal.
		if (item && slotId === EEquipmentSlot.WeaponSlot) {
			const dormant = this.weaponSwapDormantSkills(item.weaponType ?? EDamageType.Unarmed);
			if (dormant.length > 0 && !(await this.confirmWeaponSwap(item, dormant))) {
				return;
			}
		}
		if (!(await inventoryManager.equipItem(itemId, slotId))) {
			toastError('Your equipment change could not be saved. Please try again.');
		}
	}

	async unequip(slotId: EEquipmentSlot) {
		if (!(await inventoryManager.unequipItem(slotId))) {
			toastError('Your equipment change could not be saved. Please try again.');
		}
	}

	async toggleEquip(item: Item) {
		if (item.equipmentSlotId != null) {
			await this.unequip(item.equipmentSlotId);
		} else {
			await this.equip(item.itemId, getEquipmentSlotForCategory(item.itemCategoryId));
		}
	}

	toggleFavorite(itemId: number) {
		const item = this.items.find((i) => i.itemId === itemId);
		if (!item) {
			return;
		}
		inventoryManager.setFavorite(itemId, !item.favorite);
	}

	/** Unlocked mods compatible with a slot type, minus ones already on the item. */
	compatibleMods(slotType: number, item: Item): ItemMod[] {
		// eslint-disable-next-line svelte/prefer-svelte-reactivity
		const used = new Set(item.appliedMods.map((m) => m.id));
		return (staticData.itemMods ?? [])
			.filter((m) => m && m.itemModTypeId === slotType && inventoryManager.unlockedMods.has(m.id) && !used.has(m.id))
			.map((m) => ({ ...m, itemModSlotId: -1 }));
	}

	async applyMod(itemId: number, slotId: number, modId: number) {
		if (!(await inventoryManager.applyMod(itemId, modId, slotId))) {
			toastError('Your modifier change could not be saved. Please try again.');
		}
	}

	async removeMod(itemId: number, slotId: number) {
		if (!(await inventoryManager.removeMod(itemId, slotId))) {
			toastError('Your modifier change could not be saved. Please try again.');
		}
	}
}
