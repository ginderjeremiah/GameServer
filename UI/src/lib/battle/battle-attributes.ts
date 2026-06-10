import { IBattlerAttribute, EAttribute } from '$lib/api';
import { normalizeText } from '$lib/common';
import {
	STATIC_ATTRIBUTE_MODIFIERS,
	EModifierType,
	EAttributeModifierSource,
	type AttributeModifier,
	type BaseAttributeModifier
} from './attribute-modifier';
import { computeAttributes } from './attribute-collection';

const attributesMaxId = Object.values(EAttribute)[Object.values(EAttribute).length - 1] as number;

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
 *   `===` the reference the caller holds). Only {@link attributeValues} — what the UI reads — is
 *   reactive, reassigned on each recompute.
 */
export class BattleAttributes {
	/** Every modifier composing this set, in the order the backend applies them. */
	#modifiers: AttributeModifier[] = [];
	/** When false the derived/static pass is skipped — raw additive sums, for display only. */
	#calcDerived = true;
	/** The memoised per-attribute totals, recomputed only when the modifier set changes. */
	private attributeValues: number[] = new Array<number>(attributesMaxId + 1).fill(0);

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

	public getAttributeMap = (includeZeroes: boolean = false) => {
		return this.attributeValues
			.map((att, i) => ({ name: normalizeText(EAttribute[i]), value: att }))
			.filter((att) => att.value != 0 || includeZeroes);
	};

	/** Recomputes the per-attribute totals from the current modifiers. Called only when the
	 *  modifier set changes; reads then return the memoised result without recomputing. */
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
	}
}
