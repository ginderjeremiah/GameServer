import { EDamageType, EDamageTypeKey } from '$lib/api';
import { applies } from '$lib/battle/damage-types';

/*
 * Frontend display helpers for damage types (#1320/#1340). The accent hues are declared as `--dmg-*`
 * custom properties in `+layout.svelte` so they stay themeable; these helpers only reference those
 * variables (mirroring the rarity / attribute-colour helpers) and keep colour / name / icon a pure
 * frontend concern — the backend never references them.
 *
 * Metadata is keyed by `EDamageTypeKey`, the superset that adds the cross-cutting `Elemental` / `Dot`
 * categories (and the #1340 weapon leaves) on top of the leaf `EDamageType` values (so the by-type
 * attribute breakdown can label a category group). A hit's leaf `EDamageType` reuses the same metadata
 * via {@link leafKey}. The `icon` is the type's art in `static/img` (mapped like `attributeIcon`) and
 * doubles as the base for the type's amplification / resistance attribute icons, which add an amp /
 * resist badge (see `ATTRIBUTE_ICON`). The weapon leaves carry their own weapon icon but keep the
 * shared `physical` hue (they are physical-category).
 */

interface DamageTypeKeyInfo {
	/** Suffix matching the `--dmg-*` custom property (e.g. `fire`). */
	key: string;
	/** Player-facing display name, used by the breakdown group headers and combat-log prose. */
	name: string;
	/** Icon filename (in `static/img`), e.g. `Fire` → `/img/Fire.png`. */
	icon: string;
}

const DAMAGE_TYPE_KEY_INFO: Record<EDamageTypeKey, DamageTypeKeyInfo> = {
	[EDamageTypeKey.Physical]: { key: 'physical', name: 'Physical', icon: 'Physical' },
	[EDamageTypeKey.Fire]: { key: 'fire', name: 'Fire', icon: 'Fire' },
	[EDamageTypeKey.Water]: { key: 'water', name: 'Water', icon: 'Water' },
	[EDamageTypeKey.Earth]: { key: 'earth', name: 'Earth', icon: 'Earth' },
	[EDamageTypeKey.Wind]: { key: 'wind', name: 'Wind', icon: 'Wind' },
	[EDamageTypeKey.Bleed]: { key: 'bleed', name: 'Bleed', icon: 'Bleed' },
	[EDamageTypeKey.Poison]: { key: 'poison', name: 'Poison', icon: 'Poison' },
	[EDamageTypeKey.Burn]: { key: 'burn', name: 'Burn', icon: 'Burn' },
	[EDamageTypeKey.Elemental]: { key: 'elemental', name: 'Elemental', icon: 'Elemental' },
	[EDamageTypeKey.Dot]: { key: 'dot', name: 'Damage Over Time', icon: 'Damage Over Time' },
	// Weapon leaves (#1340): their own weapon icon, but the shared physical hue (physical-category).
	[EDamageTypeKey.Sword]: { key: 'physical', name: 'Sword', icon: 'Sword' },
	[EDamageTypeKey.Axe]: { key: 'physical', name: 'Axe', icon: 'Axe' },
	[EDamageTypeKey.Bow]: { key: 'physical', name: 'Bow', icon: 'Bow' },
	[EDamageTypeKey.Club]: { key: 'physical', name: 'Club', icon: 'Club' },
	[EDamageTypeKey.Dagger]: { key: 'physical', name: 'Dagger', icon: 'Dagger' },
	[EDamageTypeKey.Unarmed]: { key: 'physical', name: 'Unarmed', icon: 'Unarmed' }
};

/** The own category key of a leaf damage type — the first key its `applies()` set resolves to (a leaf
 *  type's own same-named key always leads, including the weapon leaves whose key ordinal differs from the
 *  leaf's). Robust to the append-only enum order, where a leaf and its key no longer share a numeric value. */
const leafKey = (type: EDamageType): EDamageTypeKey => applies(type)[0];

/* ── damage-type-key helpers (the breakdown groups by these, incl. Elemental/DoT) ───────────────── */

/** Themeable accent hue for a damage-type key, e.g. `var(--dmg-fire)`. */
export const damageTypeKeyColor = (key: EDamageTypeKey): string => `var(--dmg-${DAMAGE_TYPE_KEY_INFO[key].key})`;

/** Player-facing display name for a damage-type key (e.g. `Fire`, `Elemental`, `Damage Over Time`). */
export const damageTypeKeyName = (key: EDamageTypeKey): string => DAMAGE_TYPE_KEY_INFO[key].name;

/** Path to a damage-type key's icon under `static/img` (e.g. `/img/Fire.png`). */
export const damageTypeKeyIcon = (key: EDamageTypeKey): string => `/img/${DAMAGE_TYPE_KEY_INFO[key].icon}.png`;

/* ── leaf damage-type helpers (combat floaters / log key off these) ─────────────────────────────── */

/** Themeable accent hue for a leaf damage type, e.g. `var(--dmg-fire)`. */
export const damageTypeColor = (type: EDamageType): string => damageTypeKeyColor(leafKey(type));

/** Player-facing display name for a leaf damage type (e.g. `Fire`). */
export const damageTypeName = (type: EDamageType): string => damageTypeKeyName(leafKey(type));

/** Path to a leaf damage type's icon under `static/img` (e.g. `/img/Fire.png`). */
export const damageTypeIcon = (type: EDamageType): string => damageTypeKeyIcon(leafKey(type));
