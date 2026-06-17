import { EItemCategory } from '$lib/api';
import { EEquipmentSlot, getEquipmentSlotForCategory, inventoryManager } from '$lib/engine';
import { BattleAttributes, type AttributeEntry, type Item, type ItemMod } from '$lib/battle';
import { staticData, toastError } from '$stores';

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

	readonly selected = $derived(
		this.selectedId != null ? (this.items.find((i) => i.itemId === this.selectedId) ?? null) : null
	);

	readonly dragItem = $derived(
		this.dragItemId != null ? (this.items.find((i) => i.itemId === this.dragItemId) ?? null) : null
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

	// Inventory mutations apply optimistically and roll back on a persist error; await the manager's
	// boolean and surface a toast on failure so the silent revert is reported, not swallowed.
	async equip(itemId: number, slotId: EEquipmentSlot) {
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
