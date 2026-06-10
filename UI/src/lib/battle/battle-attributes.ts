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
 * The frontend's battle attribute set, mirroring the backend `AttributeCollection`. It retains
 * its modifier list (rather than flattening to a `number[]` up front) so effects can add/remove
 * modifiers mid-battle; the per-attribute totals are memoised and recomputed only when a modifier
 * changes, through the same {@link computeAttributes} path the breakdown screen uses.
 */
export class BattleAttributes {
	/** Every modifier composing this set, in the order the backend applies them. */
	private modifiers: AttributeModifier[] = [];
	/** When false the derived/static pass is skipped — raw additive sums, for display only. */
	private calcDerived = true;
	/** Memoised per-attribute totals; null marks them stale after a modifier change. */
	private cachedValues: number[] | null = null;

	constructor(attList: IBattlerAttribute[] = [], calcDerivedStats: boolean = true) {
		this.setData(attList, calcDerivedStats);
	}

	public setData(attList: IBattlerAttribute[] = [], calcDerivedStats: boolean = true) {
		this.calcDerived = calcDerivedStats;
		const base = attList.map(
			(att): BaseAttributeModifier => ({
				attribute: att.attributeId,
				amount: att.amount,
				type: EModifierType.Additive,
				source: EAttributeModifierSource.AttributeDistribution
			})
		);
		this.modifiers = calcDerivedStats ? [...base, ...STATIC_ATTRIBUTE_MODIFIERS] : base;
		this.cachedValues = null;
	}

	/** Adds a modifier and invalidates the memoised totals (recomputed lazily on next read). */
	public addModifier(modifier: AttributeModifier) {
		this.modifiers.push(modifier);
		this.cachedValues = null;
	}

	/** Removes a previously-added modifier instance (by reference). Returns whether it was present. */
	public removeModifier(modifier: AttributeModifier): boolean {
		const index = this.modifiers.indexOf(modifier);
		if (index < 0) {
			return false;
		}
		this.modifiers.splice(index, 1);
		this.cachedValues = null;
		return true;
	}

	public getValue(attId: EAttribute) {
		return this.values()[attId];
	}

	public getAttributeMap = (includeZeroes: boolean = false) => {
		return this.values()
			.map((att, i) => ({ name: normalizeText(EAttribute[i]), value: att }))
			.filter((att) => att.value != 0 || includeZeroes);
	};

	/** Lazily (re)computes and memoises the per-attribute totals from the current modifiers. */
	private values(): number[] {
		if (this.cachedValues) {
			return this.cachedValues;
		}

		const values = new Array<number>(attributesMaxId + 1).fill(0);
		if (this.calcDerived) {
			for (const [attr, result] of computeAttributes(this.modifiers)) {
				values[attr] = result.total;
			}
		} else {
			for (const modifier of this.modifiers) {
				values[modifier.attribute] += modifier.amount;
			}
		}

		this.cachedValues = values;
		return values;
	}
}
