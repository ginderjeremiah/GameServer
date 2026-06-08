import { EItemCategory } from '$lib/api';
import { EEquipmentSlot, getEquipmentSlotForCategory, inventoryManager } from '$lib/engine';
import { BattleAttributes, type Item, type ItemMod } from '$lib/battle';
import { staticData } from '$stores';

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

export interface StatEntry {
	name: string;
	value: number;
}

/* ─── Reactive view-model ────────────────────────────────────────────────
   Holds the UI state for the inventory screen. Item edits (equip, favorite,
   mods) update the local reactive copies immediately and delegate to the
   inventoryManager for persistence. */
export class InventoryView {
	items = $state<Item[]>([]);
	sort = $state<SortKey>('category');
	filterCat = $state<EItemCategory | null>(null);
	favOnly = $state(false);
	selectedId = $state<number | null>(null);
	dragItemId = $state<number | null>(null);
	page = $state(0);

	constructor() {
		this.reload();
	}

	/** Seed the reactive copies from the manager's unlocked items. */
	reload() {
		this.items = inventoryManager.unlockedItemList.map((i) => ({
			...i,
			appliedMods: [...i.appliedMods]
		}));
	}

	readonly equippedBySlot = $derived.by(() => {
		const map: Partial<Record<EEquipmentSlot, Item>> = {};
		for (const it of this.items) {
			if (it.equipmentSlotId != null) {
				map[it.equipmentSlotId as EEquipmentSlot] = it;
			}
		}
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

	readonly equippedTotals = $derived.by<StatEntry[]>(() => {
		const allAttrs = this.items
			.filter((i) => i.equipmentSlotId != null)
			.flatMap((i) => [...i.attributes, ...i.appliedMods.flatMap((m) => m.attributes)]);
		return new BattleAttributes(allAttrs, false).getAttributeMap();
	});

	readonly slotsFilled = $derived(this.items.filter((i) => i.equipmentSlotId != null).length);

	/* ── actions ─────────────────────────────────────────────────────── */

	select(itemId: number | null) {
		this.selectedId = itemId;
	}

	equip(itemId: number, slotId: EEquipmentSlot) {
		for (const it of this.items) {
			if (it.itemId === itemId) {
				it.equipmentSlotId = slotId;
				it.equipped = true;
			} else if (it.equipmentSlotId === slotId) {
				it.equipmentSlotId = undefined;
				it.equipped = false;
			}
		}
		inventoryManager.equipItem(itemId, slotId);
	}

	unequip(slotId: EEquipmentSlot) {
		for (const it of this.items) {
			if (it.equipmentSlotId === slotId) {
				it.equipmentSlotId = undefined;
				it.equipped = false;
			}
		}
		inventoryManager.unequipItem(slotId);
	}

	toggleEquip(item: Item) {
		if (item.equipmentSlotId != null) {
			this.unequip(item.equipmentSlotId);
		} else {
			this.equip(item.itemId, getEquipmentSlotForCategory(item.itemCategoryId));
		}
	}

	toggleFavorite(itemId: number) {
		const item = this.items.find((i) => i.itemId === itemId);
		if (!item) {
			return;
		}

		item.favorite = !item.favorite;
		inventoryManager.setFavorite(itemId, item.favorite);
	}

	/** Unlocked mods compatible with a slot type, minus ones already on the item. */
	compatibleMods(slotType: number, item: Item): ItemMod[] {
		// eslint-disable-next-line svelte/prefer-svelte-reactivity
		const used = new Set(item.appliedMods.map((m) => m.id));
		return (staticData.itemMods ?? [])
			.filter((m) => m && m.itemModTypeId === slotType && inventoryManager.unlockedMods.has(m.id) && !used.has(m.id))
			.map((m) => ({ ...m, itemModSlotId: -1 }));
	}

	applyMod(itemId: number, slotId: number, modId: number) {
		const item = this.items.find((i) => i.itemId === itemId);
		const modData = staticData.itemMods?.[modId];
		if (!item || !modData) {
			return;
		}

		item.appliedMods = [
			...item.appliedMods.filter((m) => m.itemModSlotId !== slotId),
			{ ...modData, itemModSlotId: slotId }
		];
		inventoryManager.applyMod(itemId, modId, slotId);
	}

	removeMod(itemId: number, slotId: number) {
		const item = this.items.find((i) => i.itemId === itemId);
		if (!item) {
			return;
		}

		item.appliedMods = item.appliedMods.filter((m) => m.itemModSlotId !== slotId);
		inventoryManager.removeMod(itemId, slotId);
	}
}
