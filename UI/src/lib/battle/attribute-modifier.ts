/* attribute-modifier.ts — the frontend's TypeScript type modeling for the backend
   attribute-modifier domain (`Game.Core/Attributes/Modifiers`).

   The enum *values* (`EModifierType` / `EAttributeModifierSource`) and the static
   modifier *table* (`STATIC_ATTRIBUTE_MODIFIERS`) are no longer hand-maintained
   mirrors: they are generated from the C# domain by `Game.Api.CodeGen`, exactly
   the way the API DTOs are — the enums into `$lib/api`'s `enums.ts`, the table into
   `$lib/api/types/attribute-modifiers.ts` (from `StaticAttributeModifiers.All`). So
   a backend-only change to a formula coefficient or an enum value can no longer
   silently desync the two implementations: the CI codegen-drift check fails on a
   stale committed table (see docs/infrastructure.md — issue #282).

   This file declares only the discriminated-union *types* used to consume that
   data (a frontend modeling choice that carries no values of its own) and pins the
   generated table to that union at the import seam below — the typed re-export is a
   compile-time conformance check that the generated values still satisfy the union. */

import { EAttribute, EModifierType, EAttributeModifierSource } from '$lib/api';
import { STATIC_ATTRIBUTE_MODIFIERS as GENERATED_STATIC_ATTRIBUTE_MODIFIERS } from '$lib/api/types/attribute-modifiers';

export { EModifierType, EAttributeModifierSource };

/** A modifier whose amount is scaled by the final value of another attribute.
 *  Properties are `readonly`, mirroring the backend's immutable `AttributeModifier`
 *  value object (#603): an instance is shared and added/removed whole, never mutated. */
export interface DerivedAttributeModifier {
	readonly attribute: EAttribute;
	readonly amount: number;
	readonly type: EModifierType;
	readonly source: EAttributeModifierSource.Derived;
	/** The attribute whose final value scales this modifier's amount. */
	readonly derivedSource: EAttribute;
}

/** A modifier with a fixed amount not derived from another attribute. Properties are
 *  `readonly`, mirroring the backend's immutable `AttributeModifier` value object (#603). */
export interface BaseAttributeModifier {
	readonly attribute: EAttribute;
	readonly amount: number;
	readonly type: EModifierType;
	readonly source: Exclude<EAttributeModifierSource, EAttributeModifierSource.Derived>;
}

/** Mirrors `Game.Core.Attributes.Modifiers.AttributeModifier`. Discriminated on
 *  `source`: only `Derived` modifiers carry a non-optional `derivedSource`. */
export type AttributeModifier = DerivedAttributeModifier | BaseAttributeModifier;

/** The engine base values and derived formulas every attribute set is built on top
 *  of, generated from `Game.Core.Attributes.Modifiers.StaticAttributeModifiers.All`
 *  and kept in the same order the backend applies them. The explicit type both
 *  documents the contract and verifies, at compile time, that the generated table
 *  conforms to the {@link AttributeModifier} union.
 *
 *  Each `BattleAttributes` spreads these into its own modifier list, so every battler
 *  shares the same modifier object references. The objects (and the table) are frozen
 *  to enforce that shared-template invariant at runtime — a complement to the union's
 *  `readonly` fields — so a stray mutation on one battler can't corrupt every other. */
export const STATIC_ATTRIBUTE_MODIFIERS: readonly AttributeModifier[] = Object.freeze(
	GENERATED_STATIC_ATTRIBUTE_MODIFIERS.map((modifier) => Object.freeze(modifier))
);
