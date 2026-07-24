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

/** The six core attributes a player directly invests stat points into — the frontend mirror of the
 *  backend `Attribute.CoreAttributes` invariant (`Game.Core/Attributes/Attribute.cs`). Every other
 *  attribute is "derived" (computed from these), so this is the meaningful measure of raw attribute
 *  investment, deliberately kept distinct from the `attributeType` display taxonomy (spike #528). */
export const CORE_ATTRIBUTES: EAttribute[] = [
	EAttribute.Strength,
	EAttribute.Endurance,
	EAttribute.Intellect,
	EAttribute.Agility,
	EAttribute.Dexterity,
	EAttribute.Luck
];

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
 *  paths. Two shared badge languages keep families consistent: the crit set uses a clean
 *  base symbol for its plain "read directly" magnitude attribute (`CriticalDamage`) and that same
 *  base symbol plus a badge for an attribute that scales something else — a `%` badge for a genuine
 *  probability (`DodgeChance`) or a `×` badge for a base-1 multiplier (`CriticalChanceMultiplier`,
 *  which scales a skill's own authored crit chance rather than being a chance itself); the
 *  damage-type amplification/resistance family (#1320/#1340) uses the type's base icon plus an
 *  `amp` (up-arrow) or `resist` (shield) badge. All badges are composited by `badge.py` so each
 *  badged variant is identical to its base bar the badge. Only the obsolete `DropBonus`, the typed
 *  DoT accumulators (Bleed/Poison/Burn DamagePerSecond, owned by the #1320 Area F UX work), the
 *  parry/dodge multiplier pair (#1457/#1523 — art pending), and the cadence pair (CooldownBonus /
 *  CooldownBonusMultiplier, #1426 — art pending) have no art and degrade to an empty icon. */
const ATTRIBUTE_ICON: Partial<Record<EAttribute, string>> = {
	[EAttribute.Strength]: 'Strength',
	[EAttribute.Endurance]: 'Endurance',
	[EAttribute.Intellect]: 'Intellect',
	[EAttribute.Agility]: 'Agility',
	[EAttribute.Dexterity]: 'Dexterity',
	[EAttribute.Luck]: 'Luck',
	[EAttribute.MaxHealth]: 'Max Health',
	[EAttribute.Toughness]: 'Toughness',
	[EAttribute.CooldownRecovery]: 'Cooldown Recovery',
	[EAttribute.CriticalChanceMultiplier]: 'Critical Chance Multiplier',
	[EAttribute.CriticalDamage]: 'Critical Damage',
	[EAttribute.DodgeChance]: 'Dodge Chance',
	[EAttribute.HealthRegenPerSecond]: 'Health Regen Per Second',
	[EAttribute.DamageReflection]: 'Damage Reflection',
	// Damage-type amplification / resistance (#1320): each type's base icon + an amp / resist badge.
	[EAttribute.PhysicalAmplification]: 'Physical Amplification',
	[EAttribute.PhysicalResistance]: 'Physical Resistance',
	[EAttribute.FireAmplification]: 'Fire Amplification',
	[EAttribute.FireResistance]: 'Fire Resistance',
	[EAttribute.WaterAmplification]: 'Water Amplification',
	[EAttribute.WaterResistance]: 'Water Resistance',
	[EAttribute.EarthAmplification]: 'Earth Amplification',
	[EAttribute.EarthResistance]: 'Earth Resistance',
	[EAttribute.WindAmplification]: 'Wind Amplification',
	[EAttribute.WindResistance]: 'Wind Resistance',
	[EAttribute.BleedAmplification]: 'Bleed Amplification',
	[EAttribute.BleedResistance]: 'Bleed Resistance',
	[EAttribute.PoisonAmplification]: 'Poison Amplification',
	[EAttribute.PoisonResistance]: 'Poison Resistance',
	[EAttribute.BurnAmplification]: 'Burn Amplification',
	[EAttribute.BurnResistance]: 'Burn Resistance',
	[EAttribute.ElementalAmplification]: 'Elemental Amplification',
	[EAttribute.ElementalResistance]: 'Elemental Resistance',
	[EAttribute.DotAmplification]: 'Damage Over Time Amplification',
	[EAttribute.DotResistance]: 'Damage Over Time Resistance',
	// Weapon-type amplification (#1340): amp-only (a weapon hit mitigates via Physical resistance).
	[EAttribute.SwordAmplification]: 'Sword Amplification',
	[EAttribute.AxeAmplification]: 'Axe Amplification',
	[EAttribute.BowAmplification]: 'Bow Amplification',
	[EAttribute.ClubAmplification]: 'Club Amplification',
	[EAttribute.DaggerAmplification]: 'Dagger Amplification',
	[EAttribute.UnarmedAmplification]: 'Unarmed Amplification'
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
