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
