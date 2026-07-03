import { describe, it, expect } from 'vitest';
import { EAttribute } from '$lib/api';
import { EModifierType, EAttributeModifierSource, STATIC_ATTRIBUTE_MODIFIERS } from '$lib/battle';

/* The `EModifierType`/`EAttributeModifierSource` enums and the
   `STATIC_ATTRIBUTE_MODIFIERS` table are generated from the C# domain by
   `Game.Api.CodeGen` (`Game.Core` `EModifierType`/`EAttributeModifierSource` and
   `Game.Core.Attributes.Modifiers.StaticAttributeModifiers.All`), and the CI
   codegen-drift check is the cross-implementation guard against a backend-only
   change going un-mirrored. These cases pin the canonical values on the frontend
   side — mirroring the backend `StaticAttributeModifiersTests` (same scenarios,
   same expected numbers) — so the generated table and enums are also anchored by
   an explicit, human-readable expectation rather than the diff check alone. */

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
			// CooldownRecovery = base 1 + 0.004·AGI + 0.001·DEX
			{
				attribute: EAttribute.CooldownRecovery,
				amount: 1,
				type: EModifierType.Additive,
				source: EAttributeModifierSource.BaseValue
			},
			{
				attribute: EAttribute.CooldownRecovery,
				amount: 0.004,
				type: EModifierType.Additive,
				source: EAttributeModifierSource.Derived,
				derivedSource: EAttribute.Agility
			},
			{
				attribute: EAttribute.CooldownRecovery,
				amount: 0.001,
				type: EModifierType.Additive,
				source: EAttributeModifierSource.Derived,
				derivedSource: EAttribute.Dexterity
			},
			// Toughness = 2·Endurance (no base, Endurance-only)
			{
				attribute: EAttribute.Toughness,
				amount: 2.0,
				type: EModifierType.Additive,
				source: EAttributeModifierSource.Derived,
				derivedSource: EAttribute.Endurance
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
			},
			// CriticalChanceMultiplier = base 1 + 0.002·LUK (#1525). Crit stays opt-in (crit rework #1425,
			// per-skill base #1453): the enabler is a skill's own authored CriticalChance (0 by default), and
			// this attribute only scales that per-skill base — so the Luck derivation is dormant (0 × mult = 0)
			// until a crit-authored skill is fielded.
			{
				attribute: EAttribute.CriticalChanceMultiplier,
				amount: 1,
				type: EModifierType.Additive,
				source: EAttributeModifierSource.BaseValue
			},
			{
				attribute: EAttribute.CriticalChanceMultiplier,
				amount: 0.002,
				type: EModifierType.Additive,
				source: EAttributeModifierSource.Derived,
				derivedSource: EAttribute.Luck
			},
			// ParryChanceMultiplier = base 1 + 0.002·LUK (#1525), the same template (#1457): the enabler is
			// the authored-only ParryChance (base 0 everywhere, so it has no static modifier).
			{
				attribute: EAttribute.ParryChanceMultiplier,
				amount: 1,
				type: EModifierType.Additive,
				source: EAttributeModifierSource.BaseValue
			},
			{
				attribute: EAttribute.ParryChanceMultiplier,
				amount: 0.002,
				type: EModifierType.Additive,
				source: EAttributeModifierSource.Derived,
				derivedSource: EAttribute.Luck
			},
			// DodgeChanceMultiplier = base 1 + 0.002·AGI (#1523), the same template: the enabler is the
			// authored-only DodgeChance (base 0 everywhere, so it has no static modifier).
			{
				attribute: EAttribute.DodgeChanceMultiplier,
				amount: 1,
				type: EModifierType.Additive,
				source: EAttributeModifierSource.BaseValue
			},
			{
				attribute: EAttribute.DodgeChanceMultiplier,
				amount: 0.002,
				type: EModifierType.Additive,
				source: EAttributeModifierSource.Derived,
				derivedSource: EAttribute.Agility
			},
			// CriticalDamage = base 1.5 + 0.0025·LUK
			{
				attribute: EAttribute.CriticalDamage,
				amount: 1.5,
				type: EModifierType.Additive,
				source: EAttributeModifierSource.BaseValue
			},
			{
				attribute: EAttribute.CriticalDamage,
				amount: 0.0025,
				type: EModifierType.Additive,
				source: EAttributeModifierSource.Derived,
				derivedSource: EAttribute.Luck
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

	it('freezes the table and its entries so the battler-shared template cannot be mutated', () => {
		// Every BattleAttributes spreads these shared object references into its own modifier list, so a
		// mutation here would corrupt every battler at once — the freeze makes that impossible at runtime.
		expect(Object.isFrozen(STATIC_ATTRIBUTE_MODIFIERS)).toBe(true);
		expect(STATIC_ATTRIBUTE_MODIFIERS.every((m) => Object.isFrozen(m))).toBe(true);
	});
});
