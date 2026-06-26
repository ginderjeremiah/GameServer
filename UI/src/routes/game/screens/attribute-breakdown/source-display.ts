/* source-display.ts — presentation metadata for the attribute-modifier sources
   shown in the breakdown.

   The hues are declared as `--source-*` custom properties in `+layout.svelte`
   (so they stay themeable); these helpers only reference those variables,
   mirroring the rarity / challenge-type / attribute helpers in `$lib/common`.
   The labels are presentation strings local to this screen; the source identity
   itself is the domain enum `EAttributeModifierSource` from `$lib/battle`. */

import { EAttributeModifierSource } from '$lib/battle';

/** Kebab key matching the `--source-*` custom properties (e.g. `points`). */
const SOURCE_KEY: Record<EAttributeModifierSource, string> = {
	[EAttributeModifierSource.BaseValue]: 'base',
	[EAttributeModifierSource.PlayerStatPoints]: 'points',
	[EAttributeModifierSource.Item]: 'item',
	[EAttributeModifierSource.ItemMod]: 'mod',
	[EAttributeModifierSource.Derived]: 'derived',
	// AttributeDistribution is an enemy/NPC source that never appears in a
	// player's breakdown; it falls back to the neutral base hue if ever shown.
	[EAttributeModifierSource.AttributeDistribution]: 'base',
	// SkillEffect is a timed battle modifier; it falls back to the derived hue.
	[EAttributeModifierSource.SkillEffect]: 'derived',
	// Proficiency is a permanent progression bonus; it is not yet surfaced in the player breakdown (that
	// lands with the proficiency client work), so it falls back to the stat-points hue until then.
	[EAttributeModifierSource.Proficiency]: 'points',
	// Class is the signature-passive bonus composed at battler assembly (#1126 area E); like the locked base
	// and proficiency bonuses it is not yet surfaced in the player breakdown, so it falls back to the
	// stat-points hue (rather than minting a speculative token) until #1261 wires those modifiers in.
	[EAttributeModifierSource.Class]: 'points'
};

const SOURCE_LABEL: Record<EAttributeModifierSource, string> = {
	[EAttributeModifierSource.BaseValue]: 'Base value',
	[EAttributeModifierSource.PlayerStatPoints]: 'Stat points',
	[EAttributeModifierSource.Item]: 'Equipment',
	[EAttributeModifierSource.ItemMod]: 'Item mods',
	[EAttributeModifierSource.Derived]: 'Derived',
	[EAttributeModifierSource.AttributeDistribution]: 'Distribution',
	[EAttributeModifierSource.SkillEffect]: 'Skill effect',
	[EAttributeModifierSource.Proficiency]: 'Proficiency',
	[EAttributeModifierSource.Class]: 'Class'
};

/** Themeable source accent hue, e.g. `var(--source-points)`. */
export const sourceColor = (source: EAttributeModifierSource): string => `var(--source-${SOURCE_KEY[source]})`;

/** Human-readable label for a source ("Stat points", "Equipment", …). */
export const sourceLabel = (source: EAttributeModifierSource): string => SOURCE_LABEL[source];
