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

   Per the project decision, only the *implemented* attributes are surfaced — the
   six core attributes plus MaxHealth / Defense / CooldownRecovery. The obsolete
   DropBonus and the not-yet-implemented crit/dodge/block attributes are omitted
   (they have no real contributors today) and will appear here automatically once
   they gain real modifiers. */

import { EAttribute, type EItemModType } from '$lib/api';
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
import { normalizeText } from '$lib/common';
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

export type AttributeGroup = 'core' | 'derived';

export interface BreakdownAttrMeta {
	id: EAttribute;
	group: AttributeGroup;
	/** Decimal places to render the value with. */
	dec: number;
}

/** The implemented attributes the breakdown surfaces, in display order. Core
 *  attributes accept allocation/gear directly; the three derived stats are the
 *  only ones the engine currently produces a real formula for. */
export const BREAKDOWN_ATTRS: BreakdownAttrMeta[] = [
	{ id: EAttribute.Strength, group: 'core', dec: 0 },
	{ id: EAttribute.Endurance, group: 'core', dec: 0 },
	{ id: EAttribute.Intellect, group: 'core', dec: 0 },
	{ id: EAttribute.Agility, group: 'core', dec: 0 },
	{ id: EAttribute.Dexterity, group: 'core', dec: 0 },
	{ id: EAttribute.Luck, group: 'core', dec: 0 },
	{ id: EAttribute.MaxHealth, group: 'derived', dec: 0 },
	{ id: EAttribute.Defense, group: 'derived', dec: 0 },
	{ id: EAttribute.CooldownRecovery, group: 'derived', dec: 2 }
];

const META_BY_ID = new Map(BREAKDOWN_ATTRS.map((m) => [m.id, m]));

export const ATTRIBUTE_GROUPS: { key: AttributeGroup; label: string }[] = [
	{ key: 'core', label: 'Core' },
	{ key: 'derived', label: 'Derived' }
];

/** Display name for an attribute, preferring the live reference data and falling
 *  back to a normalised enum key (e.g. `MaxHealth` → `Max Health`). */
export function attributeName(id: EAttribute): string {
	return staticData.attributes?.find((a) => a.id === id)?.name ?? normalizeText(EAttribute[id]);
}

/** Reference-data description for an attribute (empty when not yet loaded). */
export function attributeDescription(id: EAttribute): string {
	return staticData.attributes?.find((a) => a.id === id)?.description ?? '';
}

export function attributeMeta(id: EAttribute): BreakdownAttrMeta {
	return META_BY_ID.get(id) ?? { id, group: 'derived', dec: 0 };
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

	/** The displayed attributes grouped into Core / Derived, each with its
	 *  resolved decomposition (defaulting to zero when nothing contributes). */
	readonly groups = $derived.by(() =>
		ATTRIBUTE_GROUPS.map((g) => ({
			...g,
			attrs: BREAKDOWN_ATTRS.filter((m) => m.group === g.key).map((m) => ({
				meta: m,
				computed: this.computedFor(m.id)
			}))
		}))
	);

	readonly selectedMeta = $derived(attributeMeta(this.selected));
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
