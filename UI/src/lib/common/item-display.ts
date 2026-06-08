import { EItemCategory, EItemModType } from '$lib/api';

/*
 * Themeable presentation metadata for item categories and item-mod types: the
 * accent hues live as `--category-*` / `--mod-*` custom properties in
 * `+layout.svelte`, and these helpers reference them (mirroring `rarity.ts`).
 * They are the single source of truth for category/mod-type colours and labels
 * across the inventory and challenges screens.
 */

const CATEGORY_GROUP: Record<EItemCategory, 'armor' | 'weapon' | 'accessory'> = {
	[EItemCategory.Helm]: 'armor',
	[EItemCategory.Chest]: 'armor',
	[EItemCategory.Leg]: 'armor',
	[EItemCategory.Boot]: 'armor',
	[EItemCategory.Weapon]: 'weapon',
	[EItemCategory.Accessory]: 'accessory'
};

/** Display name of an item category, taken from the enum. */
export const itemCategoryName = (id: EItemCategory): string => EItemCategory[id] ?? 'Item';

/** Themeable category accent hue, e.g. `var(--category-weapon)`. */
export const itemCategoryColor = (id: EItemCategory): string => `var(--category-${CATEGORY_GROUP[id] ?? 'armor'})`;

const MOD_TYPE_KEY: Record<EItemModType, string> = {
	[EItemModType.Component]: 'component',
	[EItemModType.Prefix]: 'prefix',
	[EItemModType.Suffix]: 'suffix'
};

/** Display name of an item-mod type, taken from the enum. */
export const modTypeLabel = (id: EItemModType): string => EItemModType[id] ?? '';

/** Themeable item-mod-type accent hue, e.g. `var(--mod-prefix)`. */
export const modTypeColor = (id: EItemModType): string => `var(--mod-${MOD_TYPE_KEY[id] ?? 'component'})`;

/**
 * Composes an item's display name from its base name plus the names of its
 * applied prefix/suffix mods, e.g. `Flaming Iron Sword of the Bear`. Prefix mod
 * names are prepended and suffix mod names appended, both in applied order;
 * component mods do not affect the name. Kept as a pure helper (taking only the
 * mod fields it needs) so it stays testable and free of a `$lib/battle` import
 * cycle, and so the inventory grid can reuse it alongside the tooltip.
 */
export const composeItemName = (baseName: string, mods: { name: string; itemModTypeId: EItemModType }[]): string => {
	const prefixes = mods.filter((m) => m.itemModTypeId === EItemModType.Prefix).map((m) => m.name);
	const suffixes = mods.filter((m) => m.itemModTypeId === EItemModType.Suffix).map((m) => m.name);
	return [...prefixes, baseName, ...suffixes].join(' ');
};
