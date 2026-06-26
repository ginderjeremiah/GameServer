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
	// The player's class locked base — the level-scaled, non-reallocatable attribute fingerprint (#1126
	// area D). Shares the source enum with an enemy/NPC distribution but only ever appears as the player's
	// own locked base in this breakdown.
	[EAttributeModifierSource.AttributeDistribution]: 'distribution',
	// SkillEffect is a timed battle modifier; it falls back to the derived hue.
	[EAttributeModifierSource.SkillEffect]: 'derived',
	// Permanent proficiency progression bonus (#982 area E).
	[EAttributeModifierSource.Proficiency]: 'proficiency',
	// The class signature passive composed at battler assembly (#1126 area E).
	[EAttributeModifierSource.Class]: 'class'
};

const SOURCE_LABEL: Record<EAttributeModifierSource, string> = {
	[EAttributeModifierSource.BaseValue]: 'Base value',
	[EAttributeModifierSource.PlayerStatPoints]: 'Stat points',
	[EAttributeModifierSource.Item]: 'Equipment',
	[EAttributeModifierSource.ItemMod]: 'Item mods',
	[EAttributeModifierSource.Derived]: 'Derived',
	[EAttributeModifierSource.AttributeDistribution]: 'Class base',
	[EAttributeModifierSource.SkillEffect]: 'Skill effect',
	[EAttributeModifierSource.Proficiency]: 'Proficiency',
	[EAttributeModifierSource.Class]: 'Signature passive'
};

/** Themeable source accent hue, e.g. `var(--source-points)`. */
export const sourceColor = (source: EAttributeModifierSource): string => `var(--source-${SOURCE_KEY[source]})`;

/** Human-readable label for a source ("Stat points", "Equipment", …). */
export const sourceLabel = (source: EAttributeModifierSource): string => SOURCE_LABEL[source];
