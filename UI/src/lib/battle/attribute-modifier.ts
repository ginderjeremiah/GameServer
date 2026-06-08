/* attribute-modifier.ts — a faithful frontend mirror of the backend attribute
   modifier domain (`Game.Core/Attributes/Modifiers`).

   These enums and the static modifier list intentionally duplicate the C#
   domain (`EModifierType`, `EAttributeModifierSource`, `StaticAttributeModifiers`)
   rather than being produced by the API codegen: they are domain concepts, not
   API DTOs, so nothing in the wire contract carries them. They are hand-kept in
   sync with the backend and guarded by a parity test against `BattleAttributes`
   (see the attribute-collection tests) so the breakdown can never silently
   disagree with the numbers the battle simulation actually produces. */

import { EAttribute } from '$lib/api';

/** Mirrors `Game.Core.EModifierType`. Additive modifiers are applied before
 *  multiplicative ones (the backend sorts modifiers by this value). */
export enum EModifierType {
	Additive = 1,
	Multiplicative = 2
}

/** Mirrors `Game.Core.EAttributeModifierSource` — where a modifier originates. */
export enum EAttributeModifierSource {
	BaseValue = 1,
	PlayerStatPoints = 2,
	AttributeDistribution = 3,
	Derived = 4,
	Item = 5,
	ItemMod = 6
}

/** A modifier whose amount is scaled by the final value of another attribute. */
export interface DerivedAttributeModifier {
	attribute: EAttribute;
	amount: number;
	type: EModifierType;
	source: EAttributeModifierSource.Derived;
	/** The attribute whose final value scales this modifier's amount. */
	derivedSource: EAttribute;
}

/** A modifier with a fixed amount not derived from another attribute. */
export interface BaseAttributeModifier {
	attribute: EAttribute;
	amount: number;
	type: EModifierType;
	source: Exclude<EAttributeModifierSource, EAttributeModifierSource.Derived>;
}

/** Mirrors `Game.Core.Attributes.Modifiers.AttributeModifier`. Discriminated on
 *  `source`: only `Derived` modifiers carry a non-optional `derivedSource`. */
export type AttributeModifier = DerivedAttributeModifier | BaseAttributeModifier;

/** Mirrors `Game.Core.Attributes.Modifiers.StaticAttributeModifiers` plus
 *  `AttributeCollection.AddStaticModifiers` — the engine base values and derived
 *  formulas every attribute set is built on top of. Kept in the same order the
 *  backend adds them. */
export const STATIC_ATTRIBUTE_MODIFIERS: readonly AttributeModifier[] = [
	// CooldownRecovery
	{
		attribute: EAttribute.CooldownRecovery,
		amount: 0.4,
		type: EModifierType.Additive,
		source: EAttributeModifierSource.Derived,
		derivedSource: EAttribute.Agility
	},
	{
		attribute: EAttribute.CooldownRecovery,
		amount: 0.1,
		type: EModifierType.Additive,
		source: EAttributeModifierSource.Derived,
		derivedSource: EAttribute.Dexterity
	},

	// Defense
	{
		attribute: EAttribute.Defense,
		amount: 2.0,
		type: EModifierType.Additive,
		source: EAttributeModifierSource.BaseValue
	},
	{
		attribute: EAttribute.Defense,
		amount: 1.0,
		type: EModifierType.Additive,
		source: EAttributeModifierSource.Derived,
		derivedSource: EAttribute.Endurance
	},
	{
		attribute: EAttribute.Defense,
		amount: 0.5,
		type: EModifierType.Additive,
		source: EAttributeModifierSource.Derived,
		derivedSource: EAttribute.Agility
	},

	// MaxHealth
	{
		attribute: EAttribute.MaxHealth,
		amount: 50.0,
		type: EModifierType.Additive,
		source: EAttributeModifierSource.BaseValue
	},
	{
		attribute: EAttribute.MaxHealth,
		amount: 20.0,
		type: EModifierType.Additive,
		source: EAttributeModifierSource.Derived,
		derivedSource: EAttribute.Endurance
	},
	{
		attribute: EAttribute.MaxHealth,
		amount: 5.0,
		type: EModifierType.Additive,
		source: EAttributeModifierSource.Derived,
		derivedSource: EAttribute.Strength
	}
];
