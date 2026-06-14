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

import { EAttribute, EAttributeType, type EItemModType, type IAttribute } from '$lib/api';
import {
	computeAttributes,
	groupBySource,
	STATIC_ATTRIBUTE_MODIFIERS,
	EModifierType,
	EAttributeModifierSource,
	type AttributeModifier,
	type AppliedModifier,
	type ComputedAttribute
} from '$lib/battle';
import { attributeName } from '$lib/common';
import { playerManager, inventoryManager, type EEquipmentSlot } from '$lib/engine';
import { staticData } from '$stores';

/** An {@link AttributeModifier} carrying display-only provenance so the
 *  breakdown can label each contribution (which item/mod it came from). The core
 *  domain fields still mirror the backend exactly; the extra fields are ignored
 *  by the aggregation and simply flow through onto the computed lines. */
export type LabeledModifier = AttributeModifier & {
	/** The equipped item or applied mod name; absent for base/points/derived,
	 *  which are labelled by their source. */
	label?: string;
	/** The equipment slot a gear/mod contribution came from. */
	slot?: EEquipmentSlot;
	/** The mod type (Component/Prefix/Suffix) for an `ItemMod` contribution. */
	modType?: EItemModType;
};

export interface BreakdownAttrMeta {
	id: EAttribute;
	/** Display taxonomy (Primary / Secondary / Status), from the reference set. */
	type: EAttributeType;
	/** Decimal places to render the value with. */
	dec: number;
}

/** The display-taxonomy groups, in render order. Status normally stays empty here — its per-second
 *  channels only ever receive combat modifiers, which the inspector excludes — but it is listed so a
 *  Status attribute that ever gains a non-combat contributor self-selects in. */
export const ATTRIBUTE_TYPE_GROUPS: { type: EAttributeType; label: string }[] = [
	{ type: EAttributeType.Primary, label: 'Primary' },
	{ type: EAttributeType.Secondary, label: 'Secondary' },
	{ type: EAttributeType.Status, label: 'Status' }
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
		dec: attribute?.decimals ?? 0
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

/** A display-taxonomy group of displayed attributes. */
export interface BreakdownGroup {
	type: EAttributeType;
	label: string;
	attrs: BreakdownAttrEntry[];
}

/** Groups the displayed attributes — those with a non-combat contributor (see
 *  {@link hasNonCombatModifier}) — by display taxonomy, each sorted by the reference set's canonical
 *  display order, dropping empty groups. Pure over its inputs so the self-selecting membership and
 *  ordering are unit-testable independent of the live stores. */
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
		return attrs.length > 0 ? [{ type: group.type, label: group.label, attrs }] : [];
	});
}

/* ── number formatting ────────────────────────────────────────────────────── */

export function fmtNum(n: number, dec = 0): string {
	const factor = 10 ** dec;
	const rounded = dec > 0 ? Math.round(n * factor) / factor : Math.round(n);
	return rounded.toLocaleString('en-US', { minimumFractionDigits: dec, maximumFractionDigits: dec });
}

/** Signed value with a typographic minus, e.g. `+12` / `−3`. When `dec` is
 *  omitted a fractional value under 10 is shown to one decimal so small derived
 *  contributions (e.g. `+0.5`) don't collapse to `+1`/`+0`. */
export function fmtSigned(n: number, dec?: number): string {
	const places = dec ?? (Math.abs(n) < 10 && !Number.isInteger(n) ? 1 : 0);
	return (n < 0 ? '−' : '+') + fmtNum(Math.abs(n), places);
}

/* ── contribution-line labels ─────────────────────────────────────────────── */

const SLOT_LABELS = ['Helm', 'Chest', 'Leg', 'Boot', 'Weapon', 'Accessory'];

/** Short equipment-slot label (e.g. `Weapon`) for a gear/mod contribution. */
export function slotLabel(slot: number | undefined): string {
	return slot != null ? (SLOT_LABELS[slot] ?? '') : '';
}

/** The primary label for one contribution line: the source attribute for a
 *  derived line, a fixed phrase for base/stat-point lines, or the item/mod name
 *  for gear. */
export function modifierLabel(line: AppliedModifier<LabeledModifier>): string {
	switch (line.source) {
		case EAttributeModifierSource.Derived:
			return attributeName(line.derivedSource);
		case EAttributeModifierSource.PlayerStatPoints:
			return 'Allocated stat points';
		case EAttributeModifierSource.BaseValue:
			return 'Engine base value';
		default:
			return line.label ?? '';
	}
}

/** A shortened label for the dense apply-order trace (e.g. `Base`, `Stat
 *  points`, or the derived source attribute / item name). */
export function traceLabel(line: AppliedModifier<LabeledModifier>): string {
	switch (line.source) {
		case EAttributeModifierSource.Derived:
			return attributeName(line.derivedSource);
		case EAttributeModifierSource.PlayerStatPoints:
			return 'Stat points';
		case EAttributeModifierSource.BaseValue:
			return 'Base';
		default:
			return line.label ?? '';
	}
}

/* ── modifier assembly (mirrors Player.GetAllModifiers + static modifiers) ──── */

/** Builds the player's full modifier list from their live allocations and
 *  equipped loadout, then appends the engine's static base/derived modifiers —
 *  the same composition the backend's `AttributeCollection` is built from. */
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
				label: item.name,
				slot
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
					modType: mod.itemModTypeId,
					slot
				});
			}
		}
	}

	// Engine base values + derived formulas.
	for (const stat of STATIC_ATTRIBUTE_MODIFIERS) {
		mods.push({ ...stat });
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
