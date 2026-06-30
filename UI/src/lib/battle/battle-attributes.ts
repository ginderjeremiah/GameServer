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
import { foldAttributeValue } from './attribute-collection';

const attributesMaxId = Object.values(EAttribute)[Object.values(EAttribute).length - 1] as number;

/** Shared empty bucket for attributes with no stored modifiers, so a fold/sum over them is a plain 0. */
const NO_MODIFIERS: readonly AttributeModifier[] = [];

/** A single attribute's display name and value, as projected for tooltip/inventory surfaces. */
export interface AttributeEntry {
	name: string;
	value: number;
}

/**
 * The frontend's battle attribute set, mirroring the backend `AttributeCollection`. It retains its
 * modifiers (bucketed per attribute, rather than discarding them after battle start) so effects can
 * add/remove modifiers mid-battle. The per-attribute totals are memoised and, on each
 * `addModifier`/`removeModifier`, recomputed **incrementally** — only the changed attribute and the
 * attributes that transitively derive from it (via the same lazy cascade the backend
 * `AttributeCollectionNode` runs), instead of rebuilding every attribute from the full modifier list.
 * Each single-attribute value is folded through the shared {@link foldAttributeValue} kernel — the
 * value-only counterpart of the `computeAttributes` reduction the breakdown screen uses — so a battler
 * with many stacked DoT/buff modifiers pays per change only for the attributes that actually moved.
 *
 * Two reactivity rules make this safe once an instance is made reactive (`statify`):
 * - The per-attribute totals recompute is **eager** (on each `setData`/`addModifier`/`removeModifier`),
 *   so {@link getValue} is a pure read. That read happens inside Svelte `$derived` (e.g. a battler
 *   card's MaxHealth bar); a lazy total recompute would be an illegal mid-derivation `$state` write
 *   (`state_unsafe_mutation`).
 * - The modifier buckets, the derived-dependents map, the `calcDerived` flag, and the memoised display
 *   projections are **private (`#`) fields**, invisible to `statify`, so they stay non-reactive. For
 *   the buckets that keeps `removeModifier`'s reference identity intact (a reactive array would
 *   deep-proxy its elements, so the stored modifier would no longer be `===` the reference the caller
 *   holds). Only the reactive `attributeValues` — what combat reads — is reassigned on each recompute.
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
	/** Every modifier composing this set, bucketed by the attribute it modifies, in the order the
	 *  backend applies them. Mirrors `AttributeCollectionNode.Modifiers`, giving the incremental
	 *  recompute O(1) access to just the changed attribute's modifiers. */
	#buckets = new Map<EAttribute, AttributeModifier[]>();
	/** Source attribute → the attributes that have a `Derived` modifier scaled by it. Mirrors
	 *  `AttributeCollectionNode.DerivedNodes`: a change to a source cascades a recompute to these. */
	#derivedDependents = new Map<EAttribute, Set<EAttribute>>();
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
		const modifiers = calcDerivedStats
			? [...base, ...additionalModifiers, ...STATIC_ATTRIBUTE_MODIFIERS]
			: [...base, ...additionalModifiers];
		this.#buckets = new Map();
		this.#derivedDependents = new Map();
		for (const modifier of modifiers) {
			this.#index(modifier);
		}
		this.#rebuildAll();
	}

	/** Adds a modifier and recomputes only the attributes it affects. */
	public addModifier(modifier: AttributeModifier) {
		this.#index(modifier);
		this.#recomputeFrom(modifier.attribute);
	}

	/** Removes a previously-added modifier instance (by reference) and recomputes only the
	 *  attributes it affected. Returns whether it was present. */
	public removeModifier(modifier: AttributeModifier): boolean {
		const list = this.#buckets.get(modifier.attribute);
		const index = list?.indexOf(modifier) ?? -1;
		if (list === undefined || index < 0) {
			return false;
		}
		list.splice(index, 1);
		if (this.#calcDerived && modifier.source === EAttributeModifierSource.Derived) {
			this.#unlinkDerived(modifier.attribute, modifier.derivedSource);
		}
		this.#recomputeFrom(modifier.attribute);
		return true;
	}

	/** Buckets `modifier` under its attribute and, for a `Derived` modifier, records the
	 *  source → dependent cascade link. Mirrors `AttributeCollection.AddModifierWithoutCacheInvalidation`. */
	#index(modifier: AttributeModifier) {
		const list = this.#buckets.get(modifier.attribute);
		if (list === undefined) {
			this.#buckets.set(modifier.attribute, [modifier]);
		} else {
			list.push(modifier);
		}
		if (this.#calcDerived && modifier.source === EAttributeModifierSource.Derived) {
			const dependents = this.#derivedDependents.get(modifier.derivedSource);
			if (dependents === undefined) {
				this.#derivedDependents.set(modifier.derivedSource, new Set([modifier.attribute]));
			} else {
				dependents.add(modifier.attribute);
			}
		}
	}

	/** Drops the `derivedSource → dependent` cascade link, but only when no modifier still on the
	 *  dependent attribute derives from that source. Mirrors `AttributeCollection.UnhookDerivedLink`. */
	#unlinkDerived(dependent: EAttribute, derivedSource: EAttribute) {
		const list = this.#buckets.get(dependent);
		if (list !== undefined) {
			for (const modifier of list) {
				if (modifier.source === EAttributeModifierSource.Derived && modifier.derivedSource === derivedSource) {
					return;
				}
			}
		}
		this.#derivedDependents.get(derivedSource)?.delete(dependent);
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

	/** Adds `attribute` and every attribute that transitively derives from it to `dirty` — the set of
	 *  attributes whose total a change to `attribute` can move. The `dirty` guard makes a shared
	 *  dependent (reachable by more than one path) visited once and terminates the static acyclic graph. */
	#collectDependents(attribute: EAttribute, dirty: Set<EAttribute>) {
		if (dirty.has(attribute)) {
			return;
		}
		dirty.add(attribute);
		const dependents = this.#derivedDependents.get(attribute);
		if (dependents !== undefined) {
			for (const dependent of dependents) {
				this.#collectDependents(dependent, dirty);
			}
		}
	}

	/** The display-only value of an attribute: the raw additive sum of every modifier's amount, with no
	 *  type/derived semantics (the `calcDerived === false` path, where no modifier is `Derived`). */
	#rawSum(attribute: EAttribute): number {
		let sum = 0;
		for (const modifier of this.#buckets.get(attribute) ?? NO_MODIFIERS) {
			sum += modifier.amount;
		}
		return sum;
	}

	/** A memoised resolver over `dirty`: a dirty attribute is (re)folded through the shared
	 *  {@link foldAttributeValue} kernel — its `Derived` sources resolved through this same resolver —
	 *  while an unchanged attribute reads its current cached value from `values`. The in-progress guard
	 *  mirrors `computeAttributes`' circular-derived fallback (treat a re-entered attribute as 0). */
	#dirtyResolver(dirty: Set<EAttribute>, values: number[]): (attribute: EAttribute) => number {
		const recomputed = new Map<EAttribute, number>();
		const inProgress = new Set<EAttribute>();
		const resolve = (attribute: EAttribute): number => {
			if (!dirty.has(attribute)) {
				return values[attribute];
			}
			const memo = recomputed.get(attribute);
			if (memo !== undefined) {
				return memo;
			}
			if (inProgress.has(attribute)) {
				return 0;
			}
			inProgress.add(attribute);
			const value = foldAttributeValue(this.#buckets.get(attribute) ?? NO_MODIFIERS, resolve);
			inProgress.delete(attribute);
			recomputed.set(attribute, value);
			return value;
		};
		return resolve;
	}

	/** Rebuilds every attribute total from scratch — used on `setData`, where the whole modifier set
	 *  changes. The combat loop reads only `attributeValues`, so the projections rebuild on the next
	 *  UI read rather than here. */
	#rebuildAll() {
		const values = new Array<number>(attributesMaxId + 1).fill(0);
		if (this.#calcDerived) {
			const dirty = new Set<EAttribute>(this.#buckets.keys());
			const resolve = this.#dirtyResolver(dirty, values);
			for (const attribute of dirty) {
				values[attribute] = resolve(attribute);
			}
		} else {
			for (const attribute of this.#buckets.keys()) {
				values[attribute] = this.#rawSum(attribute);
			}
		}
		this.attributeValues = values;
		this.#attributeMap = null;
		this.#nonZeroAttributeMap = null;
	}

	/** Recomputes only `changed` and the attributes that transitively derive from it, leaving every
	 *  other total untouched — the incremental counterpart of {@link #rebuildAll} for the add/remove
	 *  hot path. The new array is reassigned (not mutated in place) so the memoised projections and the
	 *  `$derived` consumers reading `attributeValues` are notified exactly as on a full recompute. */
	#recomputeFrom(changed: EAttribute) {
		const values = this.attributeValues.slice();
		if (this.#calcDerived) {
			// `changed` plus everything that transitively derives from it (the cascade frontier).
			const dirty = new Set<EAttribute>();
			this.#collectDependents(changed, dirty);
			const resolve = this.#dirtyResolver(dirty, values);
			for (const attribute of dirty) {
				values[attribute] = resolve(attribute);
			}
		} else {
			// No derived links in display-only mode, so only `changed` can move.
			values[changed] = this.#rawSum(changed);
		}
		this.attributeValues = values;
		this.#attributeMap = null;
		this.#nonZeroAttributeMap = null;
	}
}
