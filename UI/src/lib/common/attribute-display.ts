import { EAttribute, EAttributeType, type IAttribute } from '$lib/api';
import { formatNum, normalizeText } from './functions';

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

/** Icon filename (in `static/img`) per attribute. Frontend-owned, like `attributeColor`/
 *  `attributeCode`: the art lives in `UI/static/img` and the backend never references these
 *  paths. The core/derived/HoT set has art; the obsolete `DropBonus`, the #1320 amp/resist family,
 *  the typed DoT accumulators, and the authored-only `DamageReflection` (its art is owned by the
 *  #1334 UX pass) have none yet and degrade to an empty icon. The crit/dodge set follows a shared
 *  visual language: the magnitude attribute (`CriticalDamage`) uses the clean base symbol, and the
 *  chance attributes reuse that symbol with a `%` badge. */
const ATTRIBUTE_ICON: Partial<Record<EAttribute, string>> = {
	[EAttribute.Strength]: 'Strength',
	[EAttribute.Endurance]: 'Endurance',
	[EAttribute.Intellect]: 'Intellect',
	[EAttribute.Agility]: 'Agility',
	[EAttribute.Dexterity]: 'Dexterity',
	[EAttribute.Luck]: 'Luck',
	[EAttribute.MaxHealth]: 'Max Health',
	// Reuses the former Defense art until dedicated Toughness art lands (#1334 owns the UX/art pass).
	[EAttribute.Toughness]: 'Defense',
	[EAttribute.CooldownRecovery]: 'Cooldown Recovery',
	[EAttribute.CriticalChance]: 'Critical Chance',
	[EAttribute.CriticalDamage]: 'Critical Damage',
	[EAttribute.DodgeChance]: 'Dodge Chance',
	[EAttribute.HealthRegenPerSecond]: 'Health Regen Per Second'
	// The typed DoT accumulators (Bleed/Poison/Burn DamagePerSecond) have no art yet — typed DoT icons
	// are owned by the #1320 Area F UX work — so they degrade to no icon (the AttributeIcon component
	// renders nothing for the empty case).
};

/** Path to the attribute's icon under `static/img`, or `''` for an attribute with no art yet.
 *  Prefer the `AttributeIcon` component, which renders nothing for the empty case. */
export const attributeIcon = (id: EAttribute): string => {
	const file = ATTRIBUTE_ICON[id];
	return file ? `/img/${file}.png` : '';
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

/** Display label for an attribute's `EAttributeType` taxonomy (`Primary` / `Secondary` / `Status`).
 *  The enum names are already display-ready, so this is the enum's reverse map; an undefined or
 *  out-of-range type (e.g. reference data not yet loaded) degrades to an empty label. */
export const attributeTypeName = (type: EAttributeType | undefined): string =>
	type != null ? (EAttributeType[type] ?? '') : '';

/** Renders an attribute value in its display form, honoring the reference set's `isPercentage`/
 *  `decimals`. A percentage attribute stores a decimal fraction (e.g. CooldownRecovery `1.09`) and
 *  renders scaled ×100 with a `%` suffix to its `decimals` precision (`109%`); other attributes render
 *  the plain number. The reference set is passed in (not read from `$stores`), mirroring the other
 *  param-based `$lib/common` helpers. */
export const formatAttributeValue = (value: number, id: EAttribute, attributes?: IAttribute[]): string => {
	const attribute = attributes?.find((a) => a.id === id);
	if (attribute?.isPercentage) {
		return `${(value * 100).toFixed(attribute.decimals)}%`;
	}
	return formatNum(value);
};

/** Renders a signed attribute delta (e.g. a per-point yield or a build-to-build change), honoring the
 *  reference set's `isPercentage` the same way as {@link formatAttributeValue}. Uses adaptive precision
 *  (trailing zeroes dropped) rather than the attribute's `decimals` so a small percentage contribution
 *  — e.g. CooldownRecovery's `+0.004` per Agility point → `+0.4%` — is not rounded away. */
export const formatAttributeDelta = (value: number, id: EAttribute, attributes?: IAttribute[]): string => {
	const isPercentage = attributes?.find((a) => a.id === id)?.isPercentage ?? false;
	const scaled = isPercentage ? value * 100 : value;
	return `${scaled < 0 ? '−' : '+'}${formatNum(Math.abs(scaled))}${isPercentage ? '%' : ''}`;
};
