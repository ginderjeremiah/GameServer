/* attribute-collection.ts — frontend mirror of `Game.Core.Attributes.AttributeCollection`.

   Aggregates a flat list of {@link AttributeModifier}s into a final value per
   attribute, exactly the way the backend does:
     · additive modifiers are applied first (running += amount), then
       multiplicative ones (running *= amount) — the backend sorts by
       `EModifierType`, so additives always precede multiplicatives;
     · a `Derived` modifier's amount is first multiplied by the *final* value of
       its `derivedSource` attribute (resolved recursively, with memoisation);
     · the running total starts at 0.

   Unlike the backend it also records, per modifier, the amount it contributed
   and the running subtotal after it, so the Attribute Breakdown screen can show
   a faithful, summing decomposition. The compute is generic over the modifier
   type so caller-supplied provenance fields (item/mod name, slot, …) flow
   through onto the produced lines. */

import { EAttribute } from '$lib/api';
import { EModifierType, EAttributeModifierSource, type AttributeModifier } from './attribute-modifier';

/** One modifier as applied during aggregation, carrying the resolved
 *  contribution and the running subtotal after it. Preserves any extra
 *  provenance fields present on the input modifier. */
export type AppliedModifier<T extends AttributeModifier = AttributeModifier> = T & {
	/** The signed amount this modifier added to the running total (for a
	 *  multiplicative step this is `running − beforeRunning`). */
	applied: number;
	/** The running subtotal immediately after this modifier was applied. */
	running: number;
	/** Whether this was a multiplicative step (vs additive). */
	multiplied: boolean;
	/** For a `Derived` modifier: the final value of its `derivedSource`. */
	derivedValue?: number;
	/** For a multiplicative modifier: the factor it multiplied by. */
	factor?: number;
};

/** The fully-resolved decomposition of a single attribute. */
export interface ComputedAttribute<T extends AttributeModifier = AttributeModifier> {
	attribute: EAttribute;
	/** Final value after every modifier. */
	total: number;
	/** Subtotal after the additive modifiers, before any multiplicative step. */
	additiveSubtotal: number;
	/** How much the multiplicative step(s) added on top of the additive subtotal. */
	multUplift: number;
	/** Every contributing modifier, in apply order (additives then multipliers). */
	lines: AppliedModifier<T>[];
}

/** Aggregates `modifiers` into a per-attribute decomposition, mirroring
 *  `AttributeCollection`. Returns a map keyed by {@link EAttribute}; attributes
 *  with no modifiers are present with a zero total and no lines. */
export function computeAttributes<T extends AttributeModifier>(
	modifiers: readonly T[]
): Map<EAttribute, ComputedAttribute<T>> {
	const byAttr = new Map<EAttribute, T[]>();
	for (const mod of modifiers) {
		const list = byAttr.get(mod.attribute);
		if (list) {
			list.push(mod);
		} else {
			byAttr.set(mod.attribute, [mod]);
		}
	}

	const cache = new Map<EAttribute, ComputedAttribute<T>>();
	const inProgress = new Set<EAttribute>();

	const valueOf = (attr: EAttribute): number => computeOne(attr).total;

	const computeOne = (attr: EAttribute): ComputedAttribute<T> => {
		const cached = cache.get(attr);
		if (cached) {
			return cached;
		}
		// Mirror the backend's circular-derived guard: if we re-enter an attribute
		// while resolving its own derived inputs, treat the in-progress value as 0
		// rather than recursing forever.
		if (inProgress.has(attr)) {
			return { attribute: attr, total: 0, additiveSubtotal: 0, multUplift: 0, lines: [] };
		}
		inProgress.add(attr);

		const list = byAttr.get(attr) ?? [];
		const adds = list.filter((m) => m.type === EModifierType.Additive);
		const mults = list.filter((m) => m.type === EModifierType.Multiplicative);

		let running = 0;
		const lines: AppliedModifier<T>[] = [];

		for (const mod of adds) {
			const derivedValue = mod.source === EAttributeModifierSource.Derived ? valueOf(mod.derivedSource!) : undefined;
			const applied = derivedValue === undefined ? mod.amount : mod.amount * derivedValue;
			running += applied;
			lines.push({ ...mod, applied, running, multiplied: false, derivedValue });
		}

		const additiveSubtotal = running;

		for (const mod of mults) {
			const derivedValue = mod.source === EAttributeModifierSource.Derived ? valueOf(mod.derivedSource!) : undefined;
			const factor = derivedValue === undefined ? mod.amount : mod.amount * derivedValue;
			const before = running;
			running *= factor;
			lines.push({ ...mod, applied: running - before, running, multiplied: true, derivedValue, factor });
		}

		const result: ComputedAttribute<T> = {
			attribute: attr,
			total: running,
			additiveSubtotal,
			multUplift: running - additiveSubtotal,
			lines
		};
		cache.set(attr, result);
		inProgress.delete(attr);
		return result;
	};

	for (const attr of byAttr.keys()) {
		computeOne(attr);
	}
	return cache;
}

/** A per-source bucket of a computed attribute's additive lines, with the
 *  source subtotal. */
export interface SourceGroup<T extends AttributeModifier = AttributeModifier> {
	source: EAttributeModifierSource;
	total: number;
	lines: AppliedModifier<T>[];
}

/** The display order sources are grouped/stacked in (base → points → gear →
 *  mods → derived), matching the backend's `EAttributeModifierSource` intent. */
export const SOURCE_ORDER: readonly EAttributeModifierSource[] = [
	EAttributeModifierSource.BaseValue,
	EAttributeModifierSource.PlayerStatPoints,
	EAttributeModifierSource.Item,
	EAttributeModifierSource.ItemMod,
	EAttributeModifierSource.Derived
];

/** Splits a computed attribute's additive lines into per-source groups (in
 *  {@link SOURCE_ORDER}) and returns the multiplicative lines separately. */
export function groupBySource<T extends AttributeModifier>(
	computed: ComputedAttribute<T>
): { groups: SourceGroup<T>[]; mults: AppliedModifier<T>[] } {
	const groups = new Map<EAttributeModifierSource, SourceGroup<T>>();
	const mults: AppliedModifier<T>[] = [];
	for (const line of computed.lines) {
		if (line.multiplied) {
			mults.push(line);
			continue;
		}
		const group = groups.get(line.source);
		if (group) {
			group.total += line.applied;
			group.lines.push(line);
		} else {
			groups.set(line.source, { source: line.source, total: line.applied, lines: [line] });
		}
	}
	const ordered = SOURCE_ORDER.filter((s) => groups.has(s)).map((s) => groups.get(s)!);
	return { groups: ordered, mults };
}
