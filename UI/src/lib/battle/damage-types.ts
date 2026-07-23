/* damage-types.ts — the frontend mirror of the backend's `Game.Core.Attributes.DamageTypes`
   (spike #1320, Area A).

   The enum *values* (`EDamageType` / `EDamageTypeKey`) and the taxonomy *tables*
   (`DAMAGE_TYPE_APPLIES`, `DAMAGE_TYPE_KEY_ATTRIBUTES`, `DAMAGE_TYPE_DOT_ACCUMULATORS`) are generated
   from the C# domain by `Game.Api.CodeGen` — the enums into `$lib/api`'s `enums.ts`, the tables into
   `$lib/api/types/damage-types.ts` (from `DamageTypes`). So a backend retune of the damage-type
   taxonomy can no longer silently desync the two simulators: the CI codegen-drift check fails on a
   stale committed table, and this module only wraps the generated tables in lookup helpers.

   The damage pipeline reads these: the direct-hit amp/resist (Area B) and the typed DoT phase (Area C).
   Iteration order is preserved from the generated tables because the damage math folds the amp/resist
   sums — and the DoT types — in that order, and float addition is not associative (a parity contract). */

import { EAttribute, EDamageType, EDamageTypeKey, type ISkillDamagePortion } from '$lib/api';
import {
	DAMAGE_TYPE_APPLIES,
	DAMAGE_TYPE_KEY_ATTRIBUTES,
	DAMAGE_TYPE_DOT_ACCUMULATORS
} from '$lib/api/types/damage-types';

/** The "primary" leaf type of a weighted damage-portion set (spike #1343): the highest-weight portion,
 *  the first in the received list on a tie (the backend mapper orders portions by damage type, so this
 *  is the lowest-numbered type on a tie). Falls back to `Physical` for a malformed empty set. Feeds the
 *  display surfaces (icon/colour) and the interim single-type direct-hit call. Mirrors the backend
 *  `Skill.PrimaryDamageType` — the strict `>` keeps the first portion winning a weight tie. */
export function primaryDamageType(portions: readonly ISkillDamagePortion[]): EDamageType {
	if (portions.length === 0) {
		return EDamageType.Physical;
	}
	let primary = portions[0];
	for (let i = 1; i < portions.length; i++) {
		if (portions[i].weight > primary.weight) {
			primary = portions[i];
		}
	}
	return primary.type;
}

/** The amplification / resistance attribute pair a damage-type key backs. The resistance is `null` for an
 *  amplification-only weapon key (#1340) — a weapon hit mitigates via the shared `Physical` key instead. */
export interface DamageTypeKeyAttributes {
	readonly amplification: EAttribute;
	readonly resistance: EAttribute | null;
}

/** A DoT leaf type paired with the per-second accumulator attribute that encodes it. */
export interface DotAccumulator {
	readonly type: EDamageType;
	readonly accumulator: EAttribute;
}

/** The set of keys whose amplification/resistance apply to a hit of the given leaf `type` — the leaf
 *  type itself plus any cross-cutting categories — in fixed iteration order. */
export function applies(type: EDamageType): readonly EDamageTypeKey[] {
	return DAMAGE_TYPE_APPLIES[type];
}

/** Whether `type` is a martial weapon-leaf (Sword/Axe/Bow/Club/Dagger/Unarmed): a non-Physical leaf that
 *  rolls up under the shared `Physical` category key. Mirrors the backend's `DamageTypes.IsWeaponLeaf`, derived
 *  from the same generated taxonomy table so the classification can't drift. What the weapon-match loadout gate
 *  keys on — a weapon's own `Item.weaponType` is not constrained to this set (any leaf is valid, e.g. a caster
 *  weapon's element). */
export function isWeaponLeaf(type: EDamageType): boolean {
	return type !== EDamageType.Physical && applies(type).includes(EDamageTypeKey.Physical);
}

/** The amplification / resistance attribute pair the given `key` backs. */
export function attributesForKey(key: EDamageTypeKey): DamageTypeKeyAttributes {
	return DAMAGE_TYPE_KEY_ATTRIBUTES[key];
}

// Amplification/resistance attribute lists per leaf type, precomputed once (mirrors the backend's
// `DamageTypes.AmplificationByType`/`ResistanceByType`) so the per-hit/per-tick lookup in the damage
// pipeline is a map read rather than rebuilding an array on every call.
const AMPLIFICATION_ATTRIBUTES_BY_TYPE: ReadonlyMap<EDamageType, readonly EAttribute[]> = new Map(
	Object.entries(DAMAGE_TYPE_APPLIES).map(([type, keys]) => [
		Number(type) as EDamageType,
		keys.map((key) => DAMAGE_TYPE_KEY_ATTRIBUTES[key].amplification)
	])
);

const RESISTANCE_ATTRIBUTES_BY_TYPE: ReadonlyMap<EDamageType, readonly EAttribute[]> = new Map(
	Object.entries(DAMAGE_TYPE_APPLIES).map(([type, keys]) => [
		Number(type) as EDamageType,
		keys
			.map((key): EAttribute | null => DAMAGE_TYPE_KEY_ATTRIBUTES[key].resistance)
			.filter((resistance): resistance is EAttribute => resistance !== null)
	])
);

/** The attacker-side amplification attributes summed for a hit of the given leaf `type`, in fixed
 *  iteration order (per-hit lookup helper for the damage pipeline). */
export function amplificationAttributes(type: EDamageType): readonly EAttribute[] {
	return AMPLIFICATION_ATTRIBUTES_BY_TYPE.get(type) ?? [];
}

/** The defender-side resistance attributes summed for a hit of the given leaf `type`, in fixed
 *  iteration order (per-hit lookup helper for the damage pipeline). Amplification-only weapon keys (#1340)
 *  contribute no resistance, so they drop out — a weapon hit resists via the shared `Physical` key only. */
export function resistanceAttributes(type: EDamageType): readonly EAttribute[] {
	return RESISTANCE_ATTRIBUTES_BY_TYPE.get(type) ?? [];
}

// The attribute → key inverse, precomputed once (mirrors the backend's `DamageTypes.KeyByAttribute`)
// so the lookup is an O(1) map read rather than a per-call scan.
const KEY_BY_ATTRIBUTE: ReadonlyMap<EAttribute, EDamageTypeKey> = new Map(
	Object.entries(DAMAGE_TYPE_KEY_ATTRIBUTES).flatMap(([key, attrs]) => {
		const damageTypeKey = Number(key) as EDamageTypeKey;
		const entries: [EAttribute, EDamageTypeKey][] = [[attrs.amplification, damageTypeKey]];
		// An amplification-only weapon key (#1340) has no resistance attribute to invert.
		if (attrs.resistance !== null) {
			entries.push([attrs.resistance, damageTypeKey]);
		}
		return entries;
	})
);

/** Inverse of {@link attributesForKey}: the damage-type key an amplification/resistance attribute
 *  belongs to, or `undefined` for any other attribute. Drives the breakdown's by-type grouping. */
export function keyForAttribute(attribute: EAttribute): EDamageTypeKey | undefined {
	return KEY_BY_ATTRIBUTE.get(attribute);
}

/** The three DoT (type, per-second accumulator) pairings in the fixed order the end-of-tick DoT phase
 *  iterates them — the single source linking a DoT leaf type to the accumulator that encodes it. */
export function dotAccumulators(): readonly DotAccumulator[] {
	return DAMAGE_TYPE_DOT_ACCUMULATORS;
}

// The accumulator → DoT type inverse, precomputed once (mirrors the backend's `DotTypeByAccumulator`).
const DOT_TYPE_BY_ACCUMULATOR: ReadonlyMap<EAttribute, EDamageType> = new Map(
	DAMAGE_TYPE_DOT_ACCUMULATORS.map((entry) => [entry.accumulator, entry.type])
);

/** The DoT leaf type a per-second `attribute` accumulates, or `undefined` when it is not a DoT
 *  accumulator. Lets the effect-apply path detect a DoT effect and freeze the caster's typed
 *  amplification into the accumulated magnitude (spike #1320, Area C). */
export function dotTypeForAccumulator(attribute: EAttribute): EDamageType | undefined {
	return DOT_TYPE_BY_ACCUMULATOR.get(attribute);
}
