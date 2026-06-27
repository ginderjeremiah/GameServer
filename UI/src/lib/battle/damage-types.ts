/* damage-types.ts — the frontend mirror of the backend's `Game.Core.Attributes.DamageTypes`
   (spike #1320, Area A).

   The enum *values* (`EDamageType` / `EDamageTypeKey`) and the taxonomy *tables*
   (`DAMAGE_TYPE_APPLIES`, `DAMAGE_TYPE_KEY_ATTRIBUTES`) are generated from the C# domain by
   `Game.Api.CodeGen` — the enums into `$lib/api`'s `enums.ts`, the tables into
   `$lib/api/types/damage-types.ts` (from `DamageTypes`). So a backend retune of the damage-type
   taxonomy can no longer silently desync the two simulators: the CI codegen-drift check fails on a
   stale committed table, and this module only wraps the generated tables in lookup helpers.

   These helpers are inert in V1 — the amp/resist attributes they resolve stay unread until the damage
   pipeline (Area B/C) consumes them. Iteration order is preserved from the generated tables because
   the damage math folds the amp/resist sums in that order and float addition is not associative (a
   parity contract). */

import { EAttribute, EDamageType, EDamageTypeKey } from '$lib/api';
import { DAMAGE_TYPE_APPLIES, DAMAGE_TYPE_KEY_ATTRIBUTES } from '$lib/api/types/damage-types';

/** The amplification / resistance attribute pair a damage-type key backs. */
export interface DamageTypeKeyAttributes {
	readonly amplification: EAttribute;
	readonly resistance: EAttribute;
}

/** The set of keys whose amplification/resistance apply to a hit of the given leaf `type` — the leaf
 *  type itself plus any cross-cutting categories — in fixed iteration order. */
export function applies(type: EDamageType): readonly EDamageTypeKey[] {
	return DAMAGE_TYPE_APPLIES[type];
}

/** The amplification / resistance attribute pair the given `key` backs. */
export function attributesForKey(key: EDamageTypeKey): DamageTypeKeyAttributes {
	return DAMAGE_TYPE_KEY_ATTRIBUTES[key];
}

/** The attacker-side amplification attributes summed for a hit of the given leaf `type`, in fixed
 *  iteration order (per-hit lookup helper for the damage pipeline). */
export function amplificationAttributes(type: EDamageType): EAttribute[] {
	return applies(type).map((key) => DAMAGE_TYPE_KEY_ATTRIBUTES[key].amplification);
}

/** The defender-side resistance attributes summed for a hit of the given leaf `type`, in fixed
 *  iteration order (per-hit lookup helper for the damage pipeline). */
export function resistanceAttributes(type: EDamageType): EAttribute[] {
	return applies(type).map((key) => DAMAGE_TYPE_KEY_ATTRIBUTES[key].resistance);
}

/** Inverse of {@link attributesForKey}: the damage-type key an amplification/resistance attribute
 *  belongs to, or `undefined` for any other attribute. Drives the breakdown's by-type grouping. */
export function keyForAttribute(attribute: EAttribute): EDamageTypeKey | undefined {
	for (const [key, attrs] of Object.entries(DAMAGE_TYPE_KEY_ATTRIBUTES)) {
		if (attrs.amplification === attribute || attrs.resistance === attribute) {
			return Number(key) as EDamageTypeKey;
		}
	}
	return undefined;
}
