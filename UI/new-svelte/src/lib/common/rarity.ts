import { ERarity } from '$lib/api';
import { tintColor } from './functions';

/*
 * Single source of truth for rarity visuals. The actual hues and per-tier glow
 * intensities are declared as `--rarity-*` / `--rarity-*-glow` custom properties
 * in `+layout.svelte` so they remain themeable; these helpers only reference
 * those variables. The grid-cell border colour and glow intensity both scale
 * with tier (white → green → blue → purple → gold → red).
 */

/** Kebab key matching the `--rarity-*` custom properties (e.g. `rare`). */
const rarityKey = (id: ERarity): string => (ERarity[id] ?? ERarity[ERarity.Common]).toLowerCase();

/** Themeable rarity hue, e.g. `var(--rarity-rare)`. */
export const rarityColor = (id: ERarity): string => `var(--rarity-${rarityKey(id)})`;

/** Themeable per-tier glow intensity (0–1, unitless), e.g. `var(--rarity-rare-glow)`. */
export const rarityGlow = (id: ERarity): string => `var(--rarity-${rarityKey(id)}-glow)`;

/** Display name of a rarity tier, taken from the enum. */
export const rarityLabel = (id: ERarity): string => ERarity[id] ?? '';

/** Numeric tier; the enum value doubles as the tier (1 = Common … 6 = Mythic). */
export const rarityLevel = (id: ERarity): number => id;

/**
 * The rarity hue at a given opacity, expressed with CSS `color-mix` so theme
 * overrides of the base hue flow through (replaces hard-coded rgba blending).
 */
export const rarityTint = (id: ERarity, alpha: number): string => tintColor(rarityColor(id), alpha);
