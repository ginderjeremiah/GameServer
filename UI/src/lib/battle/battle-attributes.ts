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
 * - The per-attribute totals recompute is **eager** (on each `setData`/`addModifier`/`removeModifier`),
 *   so {@link getValue} is a pure read. That read happens inside Svelte `$derived` (e.g. a battler
 *   card's MaxHealth bar); a lazy total recompute would be an illegal mid-derivation `$state` write
 *   (`state_unsafe_mutation`).
 * - The modifier list, the `calcDerived` flag, and the memoised display projections are **private
 *   (`#`) fields**, invisible to `statify`, so they stay non-reactive. For the modifier list that
 *   keeps `removeModifier`'s reference identity intact (a reactive array would deep-proxy its
 *   elements, so the stored modifier would no longer be `===` the reference the caller holds). Only
 *   the reactive `attributeValues` — what combat reads — is reassigned on each recompute.
 *
 * The named display projections ({@link getAttributeMap}/{@link getAttributeCount}) are nothing the
 * combat loop consumes — they back the inventory/breakdown/tooltip surfaces only — so they are built
 * **lazily on first read** and memoised, then invalidated on the next recompute. This keeps the
 * per-modifier hot path (every effect application/expiry, for both battlers, up to each tick) off the
 * per-attribute `.map` + `attributeName` reference scans; the projections rebuild once, the next time
 * a UI surface reads them. Caching them in non-reactive `#` fields makes the lazy build's write legal
 * mid-derivation (it is not a `$state` write), while a read of the reactive `attributeValues` keeps
 * the `$derived` consumers tracking changes. Names resolve through the documented {@link attributeName}
 * convention against the live `Attributes` reference set, the single source other display surfaces use.
 */
export class BattleAttributes {
	/** Every modifier composing this set, in the order the backend applies them. */
	#modifiers: AttributeModifier[] = [];
	/** When false the derived/static pass is skipped — raw additive sums, for display only. */
	#calcDerived = true;
	/** The memoised per-attribute totals, recomputed only when the modifier set changes. */
	private attributeValues: number[] = new Array<number>(attributesMaxId + 1).fill(0);
	/** Lazily-built display projection of every attribute (including zeroes); null when invalidated. */
	#attributeMap: AttributeEntry[] | null = null;
	/** Lazily-built display projection of only the non-zero attributes; null when invalidated. */
	#nonZeroAttributeMap: AttributeEntry[] | null = null;

	constructor(attList: IBattlerAttribute[] = [], calcDerivedStats: boolean = true) {
		this.setData(attList, calcDerivedStats);
	}

	/**
	 * Rebuilds the modifier set from the raw attribute amounts plus any caller-supplied
	 * `additionalModifiers` (the player's proficiency bonuses). The additional modifiers sit with the base
	 * set — **before** the static engine modifiers — mirroring the backend, where `BattleSnapshot.GetModifiers`
	 * concatenates the proficiency modifiers onto the base set and `AttributeCollection` appends
	 * `StaticAttributeModifiers` last. Keeping that order identical to the backend makes the additive
	 * accumulation bit-identical on both sides, which the anti-cheat replay depends on (#1189).
	 */
	public setData(
		attList: IBattlerAttribute[] = [],
		calcDerivedStats: boolean = true,
		additionalModifiers: readonly AttributeModifier[] = []
	) {
		this.#calcDerived = calcDerivedStats;
		const base = attList.map(
			(att): BaseAttributeModifier => ({
				attribute: att.attributeId,
				amount: att.amount,
				type: EModifierType.Additive,
				source: EAttributeModifierSource.AttributeDistribution
			})
		);
		this.#modifiers = calcDerivedStats
			? [...base, ...additionalModifiers, ...STATIC_ATTRIBUTE_MODIFIERS]
			: [...base, ...additionalModifiers];
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

	/** The named display projection — by default only the non-zero attributes; `includeZeroes`
	 *  returns every attribute. Built lazily on first read and memoised until the next recompute. */
	public getAttributeMap = (includeZeroes: boolean = false): AttributeEntry[] => {
		const { all, nonZero } = this.#ensureProjections();
		return includeZeroes ? all : nonZero;
	};

	/** The count of non-zero attributes, for consumers that only need the size. */
	public getAttributeCount = (): number => this.#ensureProjections().nonZero.length;

	/** Builds and memoises the named display projections on demand. A read of the reactive
	 *  `attributeValues` keeps `$derived` consumers tracking changes; the projections themselves cache
	 *  in non-reactive `#` fields, so this lazy write is legal mid-derivation. */
	#ensureProjections(): { all: AttributeEntry[]; nonZero: AttributeEntry[] } {
		// Must read attributeValues unconditionally (not inside the `if`): on a cache hit this read is
		// the only thing registering the $derived dependency, so consumers re-derive on recompute.
		const values = this.attributeValues;
		let all = this.#attributeMap;
		let nonZero = this.#nonZeroAttributeMap;
		if (all === null || nonZero === null) {
			all = values.map((value, id) => ({ name: attributeName(id, staticData.attributes), value }));
			nonZero = all.filter((entry) => entry.value != 0);
			this.#attributeMap = all;
			this.#nonZeroAttributeMap = nonZero;
		}
		return { all, nonZero };
	}

	/** Recomputes the per-attribute totals from the current modifiers and invalidates the lazy
	 *  display projections. Called only when the modifier set changes; the combat loop reads only
	 *  `attributeValues`, so the projections rebuild on the next UI read rather than here. */
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
		this.#attributeMap = null;
		this.#nonZeroAttributeMap = null;
	}
}
