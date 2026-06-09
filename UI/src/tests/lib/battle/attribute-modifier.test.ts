import { describe, it, expect } from 'vitest';
import { EAttribute } from '$lib/api';
import { EModifierType, EAttributeModifierSource, STATIC_ATTRIBUTE_MODIFIERS } from '$lib/battle';

/* `attribute-modifier.ts` is a hand-maintained frontend mirror of the C# domain
   (`Game.Core` `EModifierType`/`EAttributeModifierSource` and
   `Game.Core.Attributes.Modifiers.StaticAttributeModifiers`). It carries no
   behaviour, so these tests guard the mirror itself: the enum values must match
   the backend (they are sent/compared by value via the parity suite) and the
   static base/derived modifier table must encode the exact formulas the battle
   simulation builds on. A drift here would silently desync the attribute
   breakdown from the numbers the engine produces. */

describe('EModifierType', () => {
	it('matches the backend Game.Core.EModifierType values', () => {
		expect(EModifierType.Additive).toBe(1);
		expect(EModifierType.Multiplicative).toBe(2);
	});
});

describe('EAttributeModifierSource', () => {
	it('matches the backend Game.Core.EAttributeModifierSource values', () => {
		expect(EAttributeModifierSource.BaseValue).toBe(1);
		expect(EAttributeModifierSource.PlayerStatPoints).toBe(2);
		expect(EAttributeModifierSource.AttributeDistribution).toBe(3);
		expect(EAttributeModifierSource.Derived).toBe(4);
		expect(EAttributeModifierSource.Item).toBe(5);
		expect(EAttributeModifierSource.ItemMod).toBe(6);
	});
});

describe('STATIC_ATTRIBUTE_MODIFIERS', () => {
	it('mirrors Game.Core StaticAttributeModifiers exactly, in backend add order', () => {
		// Each entry corresponds 1:1 to a property in the C# StaticAttributeModifiers,
		// kept in the order AttributeCollection.AddStaticModifiers adds them.
		expect(STATIC_ATTRIBUTE_MODIFIERS).toEqual([
			// CooldownRecovery = 0.4·AGI + 0.1·DEX
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
			// Defense = base 2 + 1·END + 0.5·AGI
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
			// MaxHealth = base 50 + 20·END + 5·STR
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
		]);
	});

	it('only ever carries a derivedSource on Derived-source modifiers', () => {
		for (const mod of STATIC_ATTRIBUTE_MODIFIERS) {
			if (mod.source === EAttributeModifierSource.Derived) {
				expect(mod.derivedSource).toBeDefined();
			} else {
				expect('derivedSource' in mod).toBe(false);
			}
		}
	});

	it('declares every static modifier as additive (the engine base/derived layer)', () => {
		expect(STATIC_ATTRIBUTE_MODIFIERS.every((m) => m.type === EModifierType.Additive)).toBe(true);
	});
});
