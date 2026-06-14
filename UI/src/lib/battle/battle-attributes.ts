import { IBattlerAttribute, EAttribute } from '$lib/api';
import { attributeName } from '$lib/common';
import { staticData } from '$stores';
import {
	STATIC_ATTRIBUTE_MODIFIERS,
	EModifierType,
	EAttributeModifierSource,
	type AttributeModifier,
	type BaseAttributeModifier
} from './attribute-modifier';
import { computeAttributes } from './attribute-collection';

const attributesMaxId = Object.values(EAttribute)[Object.values(EAttribute).length - 1] as number;

/** A single attribute's display name and value, as projected for tooltip/inventory surfaces. */
export interface AttributeEntry {
	name: string;
	value: number;
}

/**
 * The frontend's battle attribute set, mirroring the backend `AttributeCollection`. It retains its
 * modifier list (rather than discarding it after battle start) so effects can add/remove modifiers
 * mid-battle; the per-attribute totals are recomputed — through the same {@link computeAttributes}
 * path the breakdown screen uses — only when the modifier set changes, and memoised between changes.
 *
 * Two reactivity rules make this safe once an instance is made reactive (`statify`):
 * - The recompute is **eager** (on each `setData`/`addModifier`/`removeModifier`), so
 *   {@link getValue}/{@link getAttributeMap} are pure reads. Those reads happen inside Svelte
 *   `$derived` (e.g. a battler card's MaxHealth bar); a lazy recompute would be an illegal
 *   mid-derivation `$state` write (`state_unsafe_mutation`).
 * - The modifier list and the `calcDerived` flag are **private (`#`) fields**, invisible to
 *   `statify`, so they stay non-reactive. That keeps `removeModifier`'s reference identity intact
 *   (a reactive array would deep-proxy its elements, so the stored modifier would no longer be
 *   `===` the reference the caller holds). Only the reactive fields — what the UI reads — are
 *   reassigned on each recompute.
 *
 * The named display projections ({@link getAttributeMap}/{@link getAttributeCount}) are memoised
 * alongside the totals (they depend only on the values), so every `$derived` tooltip/inventory
 * consumer reads a shared cached array instead of rebuilding its own per call site. Names resolve
 * through the documented {@link attributeName} convention against the live `Attributes` reference
 * set, the single source other display surfaces use.
 */
export class BattleAttributes {
	/** Every modifier composing this set, in the order the backend applies them. */
	#modifiers: AttributeModifier[] = [];
	/** When false the derived/static pass is skipped — raw additive sums, for display only. */
	#calcDerived = true;
	/** The memoised per-attribute totals, recomputed only when the modifier set changes. */
	private attributeValues: number[] = new Array<number>(attributesMaxId + 1).fill(0);
	/** Memoised display projection of every attribute (including zeroes), recomputed alongside the totals. */
	private attributeMap: AttributeEntry[] = [];
	/** Memoised display projection of only the non-zero attributes. */
	private nonZeroAttributeMap: AttributeEntry[] = [];

	constructor(attList: IBattlerAttribute[] = [], calcDerivedStats: boolean = true) {
		this.setData(attList, calcDerivedStats);
	}

	public setData(attList: IBattlerAttribute[] = [], calcDerivedStats: boolean = true) {
		this.#calcDerived = calcDerivedStats;
		const base = attList.map(
			(att): BaseAttributeModifier => ({
				attribute: att.attributeId,
				amount: att.amount,
				type: EModifierType.Additive,
				source: EAttributeModifierSource.AttributeDistribution
			})
		);
		this.#modifiers = calcDerivedStats ? [...base, ...STATIC_ATTRIBUTE_MODIFIERS] : base;
		this.recompute();
	}

	/** Adds a modifier and recomputes the totals. */
	public addModifier(modifier: AttributeModifier) {
		this.#modifiers.push(modifier);
		this.recompute();
	}

	/** Removes a previously-added modifier instance (by reference) and recomputes the totals.
	 *  Returns whether it was present. */
	public removeModifier(modifier: AttributeModifier): boolean {
		const index = this.#modifiers.indexOf(modifier);
		if (index < 0) {
			return false;
		}
		this.#modifiers.splice(index, 1);
		this.recompute();
		return true;
	}

	public getValue(attId: EAttribute) {
		return this.attributeValues[attId];
	}

	/** The memoised named projection — by default only the non-zero attributes; `includeZeroes`
	 *  returns every attribute. Both arrays are rebuilt only on recompute, so this is an O(1) read. */
	public getAttributeMap = (includeZeroes: boolean = false): AttributeEntry[] =>
		includeZeroes ? this.attributeMap : this.nonZeroAttributeMap;

	/** The memoised count of non-zero attributes, for consumers that only need the size. */
	public getAttributeCount = (): number => this.nonZeroAttributeMap.length;

	/** Recomputes the per-attribute totals and their named projections from the current modifiers.
	 *  Called only when the modifier set changes; reads then return the memoised results. */
	private recompute() {
		const values = new Array<number>(attributesMaxId + 1).fill(0);
		if (this.#calcDerived) {
			for (const [attr, result] of computeAttributes(this.#modifiers)) {
				values[attr] = result.total;
			}
		} else {
			for (const modifier of this.#modifiers) {
				values[modifier.attribute] += modifier.amount;
			}
		}
		this.attributeValues = values;
		this.attributeMap = values.map((value, id) => ({ name: attributeName(id, staticData.attributes), value }));
		this.nonZeroAttributeMap = this.attributeMap.filter((entry) => entry.value != 0);
	}
}
