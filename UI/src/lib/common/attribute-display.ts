import { EAttribute, type IAttribute } from '$lib/api';
import { normalizeText } from './functions';

/*
 * Frontend display helpers for attributes. The accent hues are declared as `--attr-*` custom
 * properties in `+layout.svelte` so they stay themeable; `attributeColor` only references those
 * variables (mirroring the rarity and challenge-type helpers) and stays a frontend/theme concern.
 * The name, short code and `isHarmful` flag come from the `Attributes` reference data — the single
 * source of truth promoted onto the backend `Attribute` model — and are passed in as a param so this
 * module stays free of a store dependency, mirroring the other param-based `$lib/common` helpers
 * (e.g. `challengeTypeName`).
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

/** Themeable core-attribute accent hue, e.g. `var(--attr-strength)`. Non-core
 *  (derived) attributes fall back to the neutral secondary text colour. */
export const attributeColor = (id: EAttribute): string => {
	const key = ATTRIBUTE_KEY[id];
	return key ? `var(--attr-${key})` : 'var(--text-secondary)';
};

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

/** The short code (e.g. `STR`) for an attribute, read from the `Attributes` reference set. Falls
 *  back to the humanised enum name when the reference data is unavailable; an attribute the backend
 *  assigns no code (most non-core attributes) resolves to its empty code verbatim. */
export const attributeCode = (id: EAttribute, attributes?: IAttribute[]): string => {
	const attribute = attributes?.find((a) => a.id === id);
	return attribute ? attribute.code : attributeEnumName(id);
};

/** Whether *raising* the attribute is detrimental to its bearer — a display-only flag driving
 *  buff/debuff tinting, read from the `Attributes` reference set. Defaults to `false` (beneficial
 *  when raised) when the reference data is unavailable. */
export const attributeIsHarmful = (id: EAttribute, attributes?: IAttribute[]): boolean =>
	attributes?.find((a) => a.id === id)?.isHarmful ?? false;
