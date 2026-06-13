import { EAttribute, type IAttribute } from '$lib/api';
import { normalizeText } from './functions';

/*
 * Single source of truth for core-attribute accent visuals and short codes. The
 * hues are declared as `--attr-*` custom properties in `+layout.svelte` so they
 * stay themeable; these helpers only reference those variables (mirroring the
 * rarity and challenge-type helpers). The three-letter codes are a stable UI
 * shorthand for the six core attributes — the full names come from the
 * `Attributes` reference data.
 */

/** Suffix matching the `--attr-*` custom properties (e.g. `strength`). */
const ATTRIBUTE_KEY: Partial<Record<EAttribute, string>> = {
	[EAttribute.Strength]: 'strength',
	[EAttribute.Endurance]: 'endurance',
	[EAttribute.Intellect]: 'intellect',
	[EAttribute.Agility]: 'agility',
	[EAttribute.Dexterity]: 'dexterity',
	[EAttribute.Luck]: 'luck'
};

/** Mono three-letter code shown beside the radar axes and allocation rows. */
const ATTRIBUTE_CODE: Partial<Record<EAttribute, string>> = {
	[EAttribute.Strength]: 'STR',
	[EAttribute.Endurance]: 'END',
	[EAttribute.Intellect]: 'INT',
	[EAttribute.Agility]: 'AGI',
	[EAttribute.Dexterity]: 'DEX',
	[EAttribute.Luck]: 'LUK'
};

/** Themeable core-attribute accent hue, e.g. `var(--attr-strength)`. Non-core
 *  (derived) attributes fall back to the neutral secondary text colour. */
export const attributeColor = (id: EAttribute): string => {
	const key = ATTRIBUTE_KEY[id];
	return key ? `var(--attr-${key})` : 'var(--text-secondary)';
};

/** The three-letter code for a core attribute (empty for derived attributes). */
export const attributeCode = (id: EAttribute): string => ATTRIBUTE_CODE[id] ?? '';

/** The humanised enum-key fallback name for an attribute (e.g. `MaxHealth` → `Max Health`),
 *  used when the live `Attributes` reference data is unavailable. An unknown/out-of-range id
 *  has no enum key, so it degrades to a readable `Unknown` rather than a blank label. */
export const attributeEnumName = (id: EAttribute): string => normalizeText(EAttribute[id]) || 'Unknown';

/**
 * The attribute's display name — the authored name from the `Attributes` reference set when
 * available, falling back to the normalised enum name. The reference set is passed in (rather
 * than read from `$stores`) so this module stays free of a store dependency, mirroring the other
 * param-based `$lib/common` helpers (e.g. `challengeTypeName`).
 */
export const attributeName = (id: EAttribute, attributes?: IAttribute[]): string =>
	attributes?.find((a) => a.id === id)?.name ?? attributeEnumName(id);
