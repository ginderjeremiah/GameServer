/* Attribute Breakdown screen — a read-only inspector that decomposes every
   implemented attribute into where its value comes from (engine base, allocated
   stat points, equipped gear, applied item mods, and derived formulas), showing
   the literal apply order the engine resolves.

   IMPORTANT: the numbers are NOT redefined here. The breakdown assembles the
   player's `AttributeModifier`s exactly the way the backend's `Player` does
   (stat points → `PlayerStatPoints`, equipped items → `Item`, applied mods →
   `ItemMod`) and aggregates them through `computeAttributes`, the frontend
   mirror of `Game.Core.Attributes.AttributeCollection`. That mirror is parity-
   tested against `BattleAttributes` (the battle calculator), so this screen can
   never disagree with the totals the simulation actually uses.

   The breakdown is self-selecting: an attribute is surfaced when it has at least one *non-combat*
   contributor (any modifier whose source is not a transient `SkillEffect`), grouped by the
   reference-data display taxonomy (Primary / Secondary / Status) and formatted with its reference
   `decimals`. Obsolete/unimplemented attributes (no contributors) and the per-second Status channels
   (combat-only modifiers) stay off the inspector, while crit/dodge/regen-gear appear automatically
   once they gain real contributors. */

import { EAttribute, EAttributeType, type EDamageTypeKey, type EItemModType, type IAttribute } from '$lib/api';
import {
	computeAttributes,
	groupBySource,
	keyForAttribute,
	playerBattleModifiers,
	STATIC_ATTRIBUTE_MODIFIERS,
	EModifierType,
	EAttributeModifierSource,
	type AttributeModifier,
	type AppliedModifier,
	type ComputedAttribute
} from '$lib/battle';
import { attributeName, damageTypeKeyName } from '$lib/common';
import { playerManager, inventoryManager } from '$lib/engine';
import { staticData, playerProficiencies } from '$stores';

/** An {@link AttributeModifier} carrying display-only provenance so the
 *  breakdown can label each contribution (which item/mod it came from). The core
 *  domain fields still mirror the backend exactly; the extra fields are ignored
 *  by the aggregation and simply flow through onto the computed lines. */
export type LabeledModifier = AttributeModifier & {
	/** The equipped item or applied mod name; absent for base/points/derived,
	 *  which are labelled by their source. */
	label?: string;
	/** The mod type (Component/Prefix/Suffix) for an `ItemMod` contribution. */
	modType?: EItemModType;
};

export interface BreakdownAttrMeta {
	id: EAttribute;
	/** Display taxonomy (Primary / Secondary / Status), from the reference set. */
	type: EAttributeType;
	/** Decimal places to render the value with. */
	dec: number;
	/** Whether the value renders as a percentage (scaled ×100 with a `%` suffix). */
	pct: boolean;
}

/** The display-taxonomy groups, in render order. Status normally stays empty here — its per-second
 *  channels only ever receive combat modifiers, which the inspector excludes — but it is listed so a
 *  Status attribute that ever gains a non-combat contributor self-selects in. */
export const ATTRIBUTE_TYPE_GROUPS: { type: EAttributeType; label: string }[] = [
	{ type: EAttributeType.Primary, label: 'Primary' },
	{ type: EAttributeType.Secondary, label: 'Secondary' },
	{ type: EAttributeType.Status, label: 'Status' },
	// The damage-type amplification/resistance family (spike #1320). Stays empty until an amp/resist
	// attribute gains a non-combat contributor, so it is invisible in V1; Area F (#1328) refines this
	// flat group into per-damage-type sub-groups via each attribute's `damageTypeKey`.
	{ type: EAttributeType.Affinity, label: 'Affinity' }
];

/** Reference-data description for an attribute (empty when not yet loaded). */
export function attributeDescription(id: EAttribute): string {
	return staticData.attributes?.find((a) => a.id === id)?.description ?? '';
}

/** An attribute's breakdown display metadata (taxonomy + precision) from the reference set, with
 *  safe fallbacks (Secondary / 0 decimals) when it is unavailable. */
export function attributeMeta(id: EAttribute, attributes: IAttribute[] | undefined): BreakdownAttrMeta {
	const attribute = attributes?.find((a) => a.id === id);
	return {
		id,
		type: attribute?.attributeType ?? EAttributeType.Secondary,
		dec: attribute?.decimals ?? 0,
		pct: attribute?.isPercentage ?? false
	};
}

/** An attribute's canonical UI ordering from the reference set, falling back to its enum value. */
function attributeDisplayOrder(id: EAttribute, attributes: IAttribute[] | undefined): number {
	return attributes?.find((a) => a.id === id)?.displayOrder ?? id;
}

/** Whether a computed attribute has at least one *non-combat* contributor — a line whose source is
 *  not a transient `SkillEffect`. This is the breakdown's self-selecting membership rule: an
 *  attribute the player has actually invested in (base/points/gear/mods/derived) is shown; one that
 *  only ever receives combat effects — or nothing — is not. */
export function hasNonCombatModifier<T extends AttributeModifier>(computed: ComputedAttribute<T>): boolean {
	return computed.lines.some((line) => line.source !== EAttributeModifierSource.SkillEffect);
}

/** One displayed attribute with its resolved metadata and decomposition. */
export interface BreakdownAttrEntry {
	meta: BreakdownAttrMeta;
	computed: ComputedAttribute<LabeledModifier>;
}

/** A display-taxonomy group of displayed attributes. The damage-type affinity family is split into one
 *  sub-group per {@link damageTypeKey} (see {@link damageTypeSubgroups}); every other taxonomy is a
 *  single group with `damageTypeKey` unset. */
export interface BreakdownGroup {
	/** Stable unique render key — the taxonomy type, or `affinity-<damageTypeKey>` for a by-type sub-group. */
	key: string;
	type: EAttributeType;
	label: string;
	/** Set for the damage-type affinity sub-groups, so the rail can tint/icon the header by its type. */
	damageTypeKey?: EDamageTypeKey;
	attrs: BreakdownAttrEntry[];
}

/** Splits the Affinity group's amplification/resistance attributes into one sub-group per damage-type
 *  key (Physical … Dot, leaf types before the Elemental/DoT categories), each labelled by its type so
 *  the large affinity set stays readable grouped under its type (#1320, Area F). Preserves each
 *  bucket's incoming display-order sort. An amp/resist attribute always resolves to a key via
 *  {@link keyForAttribute}; any future Affinity-typed attribute outside the family falls back to one
 *  untyped "Affinity" group rather than being dropped. */
function damageTypeSubgroups(entries: BreakdownAttrEntry[]): BreakdownGroup[] {
	// Pair each entry with its damage-type key (the input is already display-order sorted), then emit one
	// sub-group per distinct key in key order — each filtering its members back out in that display order.
	const keyed = entries.map((entry) => ({ entry, key: keyForAttribute(entry.meta.id) }));
	const distinctKeys = keyed
		.map((k) => k.key)
		.filter((key): key is EDamageTypeKey => key !== undefined)
		.filter((key, i, all) => all.indexOf(key) === i)
		.sort((a, b) => a - b);
	const groups: BreakdownGroup[] = distinctKeys.map((key) => ({
		key: `affinity-${key}`,
		type: EAttributeType.Affinity,
		label: damageTypeKeyName(key),
		damageTypeKey: key,
		attrs: keyed.filter((k) => k.key === key).map((k) => k.entry)
	}));
	const untyped = keyed.filter((k) => k.key === undefined).map((k) => k.entry);
	if (untyped.length > 0) {
		groups.push({ key: 'affinity', type: EAttributeType.Affinity, label: 'Affinity', attrs: untyped });
	}
	return groups;
}

/** Groups the displayed attributes — those with a non-combat contributor (see
 *  {@link hasNonCombatModifier}) — by display taxonomy, each sorted by the reference set's canonical
 *  display order, dropping empty groups. The damage-type Affinity family is further split by damage type
 *  ({@link damageTypeSubgroups}). Pure over its inputs so the self-selecting membership and ordering are
 *  unit-testable independent of the live stores. */
export function buildGroups(
	computed: Map<EAttribute, ComputedAttribute<LabeledModifier>>,
	attributes: IAttribute[] | undefined
): BreakdownGroup[] {
	const displayed = [...computed.values()].filter(hasNonCombatModifier);
	return ATTRIBUTE_TYPE_GROUPS.flatMap((group) => {
		const attrs = displayed
			.filter((c) => attributeMeta(c.attribute, attributes).type === group.type)
			.sort((a, b) => attributeDisplayOrder(a.attribute, attributes) - attributeDisplayOrder(b.attribute, attributes))
			.map((c) => ({ meta: attributeMeta(c.attribute, attributes), computed: c }));
		if (attrs.length === 0) {
			return [];
		}
		if (group.type === EAttributeType.Affinity) {
			return damageTypeSubgroups(attrs);
		}
		return [{ key: String(group.type), type: group.type, label: group.label, attrs }];
	});
}

/* ── number formatting ────────────────────────────────────────────────────── */

/** Formats a value to `dec` places. When `pct` is set the value is a decimal fraction rendered as a
 *  percentage — scaled ×100 with a `%` suffix (e.g. CooldownRecovery `1.09` → `109%`). */
export function fmtNum(n: number, dec = 0, pct = false): string {
	const value = pct ? n * 100 : n;
	const factor = 10 ** dec;
	const rounded = dec > 0 ? Math.round(value * factor) / factor : Math.round(value);
	return rounded.toLocaleString('en-US', { minimumFractionDigits: dec, maximumFractionDigits: dec }) + (pct ? '%' : '');
}

/** Signed value with a typographic minus, e.g. `+12` / `−3` (or `+8%` for a percentage attribute).
 *  When `dec` is omitted a fractional value under 10 is shown to one decimal so small contributions
 *  (e.g. `+0.5`) don't collapse to `+1`/`+0`; the threshold is judged on the rendered (percentage-
 *  scaled, when `pct`) magnitude. */
export function fmtSigned(n: number, dec?: number, pct = false): string {
	const scaled = pct ? n * 100 : n;
	const places = dec ?? (Math.abs(scaled) < 10 && !Number.isInteger(scaled) ? 1 : 0);
	return (scaled < 0 ? '−' : '+') + fmtNum(Math.abs(n), places, pct);
}

/* ── contribution-line labels ─────────────────────────────────────────────── */

/** Shared shape of a contribution-line label: both the full and the short variants resolve a derived
 *  line to its source attribute and any gear line to the item/mod name, differing only in the fixed
 *  phrasing used for the base-value and stat-point sources (`base`/`statPoints`). The class
 *  battle-assembly sources (locked base / proficiency / signature passive) have no per-line provenance
 *  to surface, so each gets a fixed phrase shared by both variants. */
function contributionLabel(line: AppliedModifier<LabeledModifier>, base: string, statPoints: string): string {
	switch (line.source) {
		case EAttributeModifierSource.Derived:
			return attributeName(line.derivedSource, staticData.attributes);
		case EAttributeModifierSource.PlayerStatPoints:
			return statPoints;
		case EAttributeModifierSource.BaseValue:
			return base;
		case EAttributeModifierSource.AttributeDistribution:
			return 'Class base';
		case EAttributeModifierSource.Proficiency:
			return 'Proficiency';
		case EAttributeModifierSource.Class:
			return 'Signature passive';
		default:
			return line.label ?? '';
	}
}

/** The primary label for one contribution line: the source attribute for a
 *  derived line, a fixed phrase for base/stat-point lines, or the item/mod name
 *  for gear. */
export function modifierLabel(line: AppliedModifier<LabeledModifier>): string {
	return contributionLabel(line, 'Engine base value', 'Allocated stat points');
}

/** A shortened label for the dense apply-order trace (e.g. `Base`, `Stat
 *  points`, or the derived source attribute / item name). */
export function traceLabel(line: AppliedModifier<LabeledModifier>): string {
	return contributionLabel(line, 'Base', 'Stat points');
}

/* ── modifier assembly (mirrors the player battler's BattleSnapshot.ToBattler) ─ */

/** Whether a modifier makes a real contribution to its attribute — a `+0` additive or a `×1`
 *  multiplicative changes no total, so it is omitted from the breakdown. It would otherwise add an
 *  empty row and, for the class signature passive's flat no-op default, spuriously self-select its
 *  attribute into the inspector (any non-combat line marks an attribute as displayed). */
function contributes(modifier: AttributeModifier): boolean {
	return modifier.type === EModifierType.Multiplicative ? modifier.amount !== 1 : modifier.amount !== 0;
}

/** Builds the player's full modifier list the exact way the live player battler is assembled
 *  (`BattleEngine.resetPlayer` → the backend's `BattleSnapshot.ToBattler`): allocated stat points →
 *  equipped gear → class locked base → proficiency bonuses → engine static base/derived → the class
 *  signature passive last. Keeping this order identical to the battler is what lets the inspector
 *  aggregate (through `computeAttributes`) to the same totals the simulation uses — float addition is
 *  not associative, so the apply order is load-bearing. */
export function buildPlayerModifiers(): LabeledModifier[] {
	const mods: LabeledModifier[] = [];

	// Allocated stat points (additive). Zero allocations contribute nothing and
	// would only add empty rows, so they are skipped.
	for (const alloc of playerManager.attributes) {
		if (alloc.amount !== 0) {
			mods.push({
				attribute: alloc.attributeId,
				amount: alloc.amount,
				type: EModifierType.Additive,
				source: EAttributeModifierSource.PlayerStatPoints
			});
		}
	}

	// Equipped items and their applied mods. Item/mod attributes are additive on
	// the backend (see ItemMapper); the frontend has no modifier-type info on a
	// raw IBattlerAttribute, so they are modelled as additive accordingly.
	const slots = inventoryManager.equippedSlots;
	for (let slot = 0; slot < slots.length; slot++) {
		const item = slots[slot];
		if (!item) {
			continue;
		}
		for (const attr of item.attributes) {
			mods.push({
				attribute: attr.attributeId,
				amount: attr.amount,
				type: EModifierType.Additive,
				source: EAttributeModifierSource.Item,
				label: item.name
			});
		}
		for (const mod of item.appliedMods) {
			for (const attr of mod.attributes) {
				mods.push({
					attribute: attr.attributeId,
					amount: attr.amount,
					type: EModifierType.Additive,
					source: EAttributeModifierSource.ItemMod,
					label: mod.name,
					modType: mod.itemModTypeId
				});
			}
		}
	}

	// Class locked base (the level-scaled attribute fingerprint, source `AttributeDistribution`) then the
	// proficiency bonuses (source `Proficiency`) — both composed before the static engine modifiers, the
	// order the battler assembles them in. The shared `playerBattleModifiers` builder is the single source
	// of this pairing so the battle engine and the skills screen can't drift from it (#1500).
	for (const mod of playerBattleModifiers(
		playerManager.battleLockedBaseModifiers,
		playerProficiencies.battleModifiers
	)) {
		if (contributes(mod)) {
			mods.push({ ...mod });
		}
	}

	// Engine base values + derived formulas.
	for (const stat of STATIC_ATTRIBUTE_MODIFIERS) {
		mods.push({ ...stat });
	}

	// The class signature passive is composed LAST (after the locked base, proficiency, and statics),
	// resolved against the already-assembled set — an inline mirror of `applySignaturePassive`
	// ($lib/battle/player-battle-composition, the canonical ordering the battler and skills screen share),
	// kept inline because this surface needs the labeled modifier itself, resolved over the
	// `computeAttributes` fold (lazily, so a flat non-scaled passive skips the resolve pass).
	let resolved: Map<EAttribute, ComputedAttribute<LabeledModifier>> | undefined;
	const passive = playerManager.battleSignaturePassiveModifier((attribute) => {
		resolved ??= computeAttributes(mods);
		return resolved.get(attribute)?.total ?? 0;
	});
	if (contributes(passive)) {
		mods.push({ ...passive });
	}

	return mods;
}

const zeroComputed = (attr: EAttribute): ComputedAttribute<LabeledModifier> => ({
	attribute: attr,
	total: 0,
	additiveSubtotal: 0,
	multUplift: 0,
	lines: []
});

/* ── reactive view-model ──────────────────────────────────────────────────── */

export class AttributeBreakdownView {
	/** The attribute whose decomposition is expanded on the right. Defaults to
	 *  MaxHealth — the most interesting derived stat to inspect. */
	selected = $state<EAttribute>(EAttribute.MaxHealth);

	/** The player's full modifier list, rebuilt when allocations/equipment change. */
	readonly modifiers = $derived.by<LabeledModifier[]>(() => buildPlayerModifiers());

	/** Per-attribute decompositions, keyed by attribute. */
	readonly computed = $derived.by(() => computeAttributes(this.modifiers));

	/** The displayed attributes grouped by display taxonomy (Primary / Secondary / Status), each
	 *  self-selected by having a non-combat contributor and sorted by canonical display order. */
	readonly groups = $derived.by(() => buildGroups(this.computed, staticData.attributes));

	readonly selectedMeta = $derived(attributeMeta(this.selected, staticData.attributes));
	readonly selectedComputed = $derived(this.computedFor(this.selected));
	readonly selectedGrouped = $derived(groupBySource(this.selectedComputed));

	/** The resolved decomposition for `attr`, or a zero stub if nothing
	 *  contributes (so every displayed attribute still renders). */
	computedFor(attr: EAttribute): ComputedAttribute<LabeledModifier> {
		return this.computed.get(attr) ?? zeroComputed(attr);
	}

	select(attr: EAttribute): void {
		this.selected = attr;
	}
}
