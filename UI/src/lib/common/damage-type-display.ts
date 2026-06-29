import { EDamageType, EDamageTypeKey } from '$lib/api';

/*
 * Frontend display helpers for damage types (#1320, Area F). The accent hues are declared as `--dmg-*`
 * custom properties in `+layout.svelte` so they stay themeable; these helpers only reference those
 * variables (mirroring the rarity / attribute-colour helpers) and keep colour/name/glyph a pure
 * frontend concern — the backend never references them.
 *
 * Metadata is keyed by `EDamageTypeKey`, the superset that adds the cross-cutting `Elemental` / `Dot`
 * categories on top of the eight leaf `EDamageType` values (so the by-type attribute breakdown can
 * label a category group). A hit's leaf `EDamageType` reuses the same metadata: the eight leaf values
 * are codegen'd from the same backend taxonomy as `EDamageTypeKey` and therefore share their numeric
 * value (e.g. `EDamageType.Fire === EDamageTypeKey.Fire`), so a leaf type indexes the key-keyed maps
 * directly — see {@link leafKey}.
 */

/** The inline-SVG glyph variants drawn by `DamageTypeGlyph` — one per damage-type key. */
export type DamageGlyph =
	| 'physical'
	| 'fire'
	| 'water'
	| 'earth'
	| 'wind'
	| 'bleed'
	| 'poison'
	| 'burn'
	| 'elemental'
	| 'dot';

interface DamageTypeKeyInfo {
	/** Suffix matching the `--dmg-*` custom property (e.g. `fire`). */
	key: string;
	/** Player-facing display name, used by the breakdown group headers and combat-log prose. */
	name: string;
	glyph: DamageGlyph;
}

const DAMAGE_TYPE_KEY_INFO: Record<EDamageTypeKey, DamageTypeKeyInfo> = {
	[EDamageTypeKey.Physical]: { key: 'physical', name: 'Physical', glyph: 'physical' },
	[EDamageTypeKey.Fire]: { key: 'fire', name: 'Fire', glyph: 'fire' },
	[EDamageTypeKey.Water]: { key: 'water', name: 'Water', glyph: 'water' },
	[EDamageTypeKey.Earth]: { key: 'earth', name: 'Earth', glyph: 'earth' },
	[EDamageTypeKey.Wind]: { key: 'wind', name: 'Wind', glyph: 'wind' },
	[EDamageTypeKey.Bleed]: { key: 'bleed', name: 'Bleed', glyph: 'bleed' },
	[EDamageTypeKey.Poison]: { key: 'poison', name: 'Poison', glyph: 'poison' },
	[EDamageTypeKey.Burn]: { key: 'burn', name: 'Burn', glyph: 'burn' },
	[EDamageTypeKey.Elemental]: { key: 'elemental', name: 'Elemental', glyph: 'elemental' },
	[EDamageTypeKey.Dot]: { key: 'dot', name: 'Damage Over Time', glyph: 'dot' }
};

/** The own category key of a leaf damage type. The eight leaf `EDamageType` values share their numeric
 *  value with the matching `EDamageTypeKey` (same backend taxonomy), so the leaf type *is* its own key. */
const leafKey = (type: EDamageType): EDamageTypeKey => type as unknown as EDamageTypeKey;

/* ── damage-type-key helpers (the breakdown groups by these, incl. Elemental/DoT) ───────────────── */

/** Themeable accent hue for a damage-type key, e.g. `var(--dmg-fire)`. */
export const damageTypeKeyColor = (key: EDamageTypeKey): string => `var(--dmg-${DAMAGE_TYPE_KEY_INFO[key].key})`;

/** Player-facing display name for a damage-type key (e.g. `Fire`, `Elemental`, `Damage Over Time`). */
export const damageTypeKeyName = (key: EDamageTypeKey): string => DAMAGE_TYPE_KEY_INFO[key].name;

/** The `DamageTypeGlyph` variant for a damage-type key. */
export const damageTypeKeyGlyph = (key: EDamageTypeKey): DamageGlyph => DAMAGE_TYPE_KEY_INFO[key].glyph;

/* ── leaf damage-type helpers (combat floaters / log key off these) ─────────────────────────────── */

/** Themeable accent hue for a leaf damage type, e.g. `var(--dmg-fire)`. */
export const damageTypeColor = (type: EDamageType): string => damageTypeKeyColor(leafKey(type));

/** Player-facing display name for a leaf damage type (e.g. `Fire`). */
export const damageTypeName = (type: EDamageType): string => damageTypeKeyName(leafKey(type));

/** The `DamageTypeGlyph` variant for a leaf damage type. */
export const damageTypeGlyph = (type: EDamageType): DamageGlyph => damageTypeKeyGlyph(leafKey(type));
