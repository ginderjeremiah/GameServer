/* source-display.ts — presentation metadata for the attribute-modifier sources
   shown in the breakdown.

   The hues are declared as `--source-*` custom properties in `+layout.svelte`
   (so they stay themeable); these helpers only reference those variables,
   mirroring the rarity / challenge-type / attribute helpers in `$lib/common`.
   The labels are presentation strings local to this screen; the source identity
   itself is the domain enum `EAttributeModifierSource` from `$lib/battle`. */

import { EAttributeModifierSource } from '$lib/battle';
import { tintColor } from '$lib/common';

/** Kebab key matching the `--source-*` custom properties (e.g. `points`). */
const SOURCE_KEY: Record<EAttributeModifierSource, string> = {
	[EAttributeModifierSource.BaseValue]: 'base',
	[EAttributeModifierSource.PlayerStatPoints]: 'points',
	[EAttributeModifierSource.Item]: 'item',
	[EAttributeModifierSource.ItemMod]: 'mod',
	[EAttributeModifierSource.Derived]: 'derived',
	// AttributeDistribution is an enemy/NPC source that never appears in a
	// player's breakdown; it falls back to the neutral base hue if ever shown.
	[EAttributeModifierSource.AttributeDistribution]: 'base'
};

const SOURCE_LABEL: Record<EAttributeModifierSource, string> = {
	[EAttributeModifierSource.BaseValue]: 'Base value',
	[EAttributeModifierSource.PlayerStatPoints]: 'Stat points',
	[EAttributeModifierSource.Item]: 'Equipment',
	[EAttributeModifierSource.ItemMod]: 'Item mods',
	[EAttributeModifierSource.Derived]: 'Derived',
	[EAttributeModifierSource.AttributeDistribution]: 'Distribution'
};

/** Themeable source accent hue, e.g. `var(--source-points)`. */
export const sourceColor = (source: EAttributeModifierSource): string => `var(--source-${SOURCE_KEY[source]})`;

/** The source accent at a given opacity (themeable via `color-mix`). */
export const sourceTint = (source: EAttributeModifierSource, alpha: number): string =>
	tintColor(sourceColor(source), alpha);

/** Human-readable label for a source ("Stat points", "Equipment", …). */
export const sourceLabel = (source: EAttributeModifierSource): string => SOURCE_LABEL[source];
