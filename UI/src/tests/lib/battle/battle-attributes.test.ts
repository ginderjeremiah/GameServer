import { describe, it, expect, beforeEach, vi } from 'vitest';
import { EAttribute, type IAttribute } from '$lib/api';
import { BattleAttributes } from '$lib/battle/battle-attributes';
import {
	STATIC_ATTRIBUTE_MODIFIERS,
	EModifierType,
	EAttributeModifierSource,
	type AttributeModifier
} from '$lib/battle/attribute-modifier';

// The projection resolves names through `attributeName(id, staticData.attributes)`, so mock the
// store to drive that reference set. Empty by default → names fall back to the normalised enum.
const { mockAttributes } = vi.hoisted(() => ({ mockAttributes: [] as IAttribute[] }));

vi.mock('$stores', () => ({
	staticData: {
		get attributes() {
			return mockAttributes;
		}
	}
}));

const makeAttrs = (...pairs: [EAttribute, number][]) => pairs.map(([attributeId, amount]) => ({ attributeId, amount }));

const additive = (attribute: EAttribute, amount: number): AttributeModifier => ({
	attribute,
	amount,
	type: EModifierType.Additive,
	source: EAttributeModifierSource.PlayerStatPoints
});

const derived = (attribute: EAttribute, amount: number, derivedSource: EAttribute): AttributeModifier => ({
	attribute,
	amount,
	type: EModifierType.Additive,
	source: EAttributeModifierSource.Derived,
	derivedSource
});

describe('BattleAttributes', () => {
	describe('constructor', () => {
		it('initializes with zero values when no attributes provided', () => {
			const ba = new BattleAttributes([], false);
			expect(ba.getValue(EAttribute.Strength)).toBe(0);
			expect(ba.getValue(EAttribute.Endurance)).toBe(0);
		});

		it('sets base attribute values from the list', () => {
			const ba = new BattleAttributes(makeAttrs([EAttribute.Strength, 50], [EAttribute.Agility, 20]), false);
			expect(ba.getValue(EAttribute.Strength)).toBe(50);
			expect(ba.getValue(EAttribute.Agility)).toBe(20);
		});

		it('accumulates duplicate attribute entries', () => {
			const ba = new BattleAttributes(makeAttrs([EAttribute.Strength, 10], [EAttribute.Strength, 5]), false);
			expect(ba.getValue(EAttribute.Strength)).toBe(15);
		});

		it('calculates derived stats by default', () => {
			const ba = new BattleAttributes(makeAttrs([EAttribute.Endurance, 10]));
			expect(ba.getValue(EAttribute.MaxHealth)).toBeGreaterThan(0);
		});

		it('skips derived stats when calcDerivedStats is false', () => {
			const ba = new BattleAttributes(makeAttrs([EAttribute.Endurance, 10]), false);
			expect(ba.getValue(EAttribute.MaxHealth)).toBe(0);
		});
	});

	// Parity guard for the shared derived-stat formulas. Every scenario here MUST be mirrored — with
	// identical inputs (the same name and core-attribute allocations) — in the backend suite
	// Game.Core.Tests/Attributes/BattleAttributesParityTests.cs, just as the battle-simulation parity
	// suite shares its scenario table row-for-row. The expected value is NOT a re-hardcoded literal: it
	// is composed from the single source of truth — STATIC_ATTRIBUTE_MODIFIERS (generated from the
	// backend's StaticAttributeModifiers.All) — by expectedValue, so a coefficient retune flows into both
	// the production BattleAttributes path and the expectation rather than a second copy that could rot.
	describe('derived stat computation (parity)', () => {
		type Allocation = [EAttribute, number];
		interface DerivedStatScenario {
			allocations: Allocation[];
			derivedAttributes: EAttribute[];
		}

		const scenarios: Record<string, DerivedStatScenario> = {
			// MaxHealth = 50 (base) + 20*Endurance + 5*Strength
			maxHealth: {
				allocations: [
					[EAttribute.Strength, 10],
					[EAttribute.Endurance, 20]
				],
				derivedAttributes: [EAttribute.MaxHealth]
			},

			// Toughness = 2·Endurance (no base, Endurance-only)
			toughness: {
				allocations: [[EAttribute.Endurance, 30]],
				derivedAttributes: [EAttribute.Toughness]
			},

			// CooldownRecovery = 1 (base) + 0.004*Agility + 0.001*Dexterity
			cooldownRecovery: {
				allocations: [
					[EAttribute.Agility, 20],
					[EAttribute.Dexterity, 10]
				],
				derivedAttributes: [EAttribute.CooldownRecovery]
			},

			// CriticalChanceMultiplier is opt-in (crit rework #1425, per-skill base #1453): a base of 1 (like
			// CooldownRecovery) and no attribute derivation, so even a heavy Dexterity/Luck build sits at
			// exactly 1 — DEX/LUK no longer feed crit, and the enabler is a skill's own authored base chance,
			// not this attribute. The opt-in-multiplicative math is covered in attribute-collection.test.ts.
			criticalChanceMultiplier: {
				allocations: [
					[EAttribute.Dexterity, 20],
					[EAttribute.Luck, 10]
				],
				derivedAttributes: [EAttribute.CriticalChanceMultiplier]
			},

			// CriticalDamage = 1.5 (base) + 0.0025*Luck
			criticalDamage: {
				allocations: [[EAttribute.Luck, 20]],
				derivedAttributes: [EAttribute.CriticalDamage]
			},

			// DodgeChance = 0.001*Agility (no base)
			dodgeChance: {
				allocations: [[EAttribute.Agility, 20]],
				derivedAttributes: [EAttribute.DodgeChance]
			},

			// With no allocations every derived stat collapses to just its base: the three with a base carry
			// it (MaxHealth 50, CriticalDamage 1.5, CriticalChanceMultiplier 1), the pure-derived stats
			// (Toughness, CooldownRecovery's coefficients aside, and DodgeChance) are 0/base. DamageReflection
			// is authored-only (no static modifier at all), so it is 0 here too.
			zeroBaseStats: {
				allocations: [],
				derivedAttributes: [
					EAttribute.MaxHealth,
					EAttribute.Toughness,
					EAttribute.CooldownRecovery,
					EAttribute.CriticalDamage,
					EAttribute.DamageReflection,
					EAttribute.CriticalChanceMultiplier,
					EAttribute.DodgeChance
				]
			}
		};

		// Composes the expected value of `attribute` directly from STATIC_ATTRIBUTE_MODIFIERS (the single
		// source of truth) under the given allocations — the same reduction BattleAttributes performs.
		// Every static modifier is additive: a base value contributes its raw amount, a derived modifier
		// contributes amount * allocation[derivedSource] (an allocated core attribute carries no further
		// modifiers, so its final value is its raw amount).
		const expectedValue = (attribute: EAttribute, allocations: Allocation[]): number => {
			const allocated = new Map(allocations);
			return STATIC_ATTRIBUTE_MODIFIERS.filter((modifier) => modifier.attribute === attribute).reduce(
				(sum, modifier) => {
					expect(modifier.type).toBe(EModifierType.Additive);
					return modifier.source === EAttributeModifierSource.Derived
						? sum + modifier.amount * (allocated.get(modifier.derivedSource) ?? 0)
						: sum + modifier.amount;
				},
				0
			);
		};

		it.each(Object.keys(scenarios))('composes %s derived stats from the single-sourced coefficients', (name) => {
			const { allocations, derivedAttributes } = scenarios[name];
			const ba = new BattleAttributes(makeAttrs(...allocations));

			for (const attribute of derivedAttributes) {
				// toBeCloseTo to 10 decimals: the fractional coefficients accumulate the usual binary-float
				// error, matching the backend's `Assert.Equal(expected, actual, 10)` tolerance.
				expect(ba.getValue(attribute)).toBeCloseTo(expectedValue(attribute, allocations), 10);
			}
		});
	});

	describe('setData', () => {
		it('resets attributes before applying new data', () => {
			const ba = new BattleAttributes(makeAttrs([EAttribute.Strength, 50]), false);
			ba.setData(makeAttrs([EAttribute.Agility, 30]), false);

			expect(ba.getValue(EAttribute.Strength)).toBe(0);
			expect(ba.getValue(EAttribute.Agility)).toBe(30);
		});
	});

	describe('getAttributeMap', () => {
		// Reset the reference set between tests so each controls its own name resolution.
		beforeEach(() => {
			mockAttributes.length = 0;
		});

		it('returns only non-zero attributes by default', () => {
			const ba = new BattleAttributes(makeAttrs([EAttribute.Strength, 10]), false);
			const map = ba.getAttributeMap();

			expect(map.some((a) => a.name === 'Strength')).toBe(true);
			expect(map.every((a) => a.value !== 0)).toBe(true);
		});

		it('includes zero attributes when includeZeroes is true', () => {
			const ba = new BattleAttributes([], false);
			const map = ba.getAttributeMap(true);

			expect(map.length).toBeGreaterThan(0);
			expect(map.some((a) => a.value === 0)).toBe(true);
		});

		it('matches the prior projection: full map mirrors every value, non-zero map filters it', () => {
			const ba = new BattleAttributes(makeAttrs([EAttribute.Strength, 10], [EAttribute.Agility, 4]), false);
			const full = ba.getAttributeMap(true);
			const nonZero = ba.getAttributeMap();

			// The full map carries one entry per attribute id, value-aligned with getValue.
			full.forEach((entry, id) => {
				expect(entry.value).toBe(ba.getValue(id));
			});
			expect(nonZero).toEqual(full.filter((e) => e.value !== 0));
			expect(nonZero).toEqual([
				{ name: 'Strength', value: 10 },
				{ name: 'Agility', value: 4 }
			]);
		});

		it('memoises each projection: repeated reads return the same array reference', () => {
			const ba = new BattleAttributes(makeAttrs([EAttribute.Strength, 10]), false);

			expect(ba.getAttributeMap()).toBe(ba.getAttributeMap());
			expect(ba.getAttributeMap(true)).toBe(ba.getAttributeMap(true));
		});

		it('does not rebuild the projection on the modifier hot path until it is read again', async () => {
			const { staticData } = await import('$stores');
			const ba = new BattleAttributes(makeAttrs([EAttribute.Strength, 10]), false);

			// First read builds the projection, scanning the reference set once per attribute.
			ba.getAttributeMap();
			const getter = vi.spyOn(staticData, 'attributes', 'get');

			// The combat hot path (add/remove modifiers) only touches the totals — no projection scans.
			const transient = additive(EAttribute.Agility, 6);
			ba.addModifier(transient);
			ba.removeModifier(transient);
			ba.addModifier(additive(EAttribute.Agility, 3));
			expect(getter).not.toHaveBeenCalled();

			// The next UI read rebuilds it lazily, reflecting the accumulated changes.
			const rebuilt = ba.getAttributeMap();
			expect(getter).toHaveBeenCalled();
			expect(rebuilt).toEqual([
				{ name: 'Strength', value: 10 },
				{ name: 'Agility', value: 3 }
			]);

			getter.mockRestore();
		});

		it('rebuilds the projection after a modifier change', () => {
			const ba = new BattleAttributes(makeAttrs([EAttribute.Strength, 10]), false);
			const before = ba.getAttributeMap();
			expect(before).toEqual([{ name: 'Strength', value: 10 }]);

			ba.addModifier(additive(EAttribute.Agility, 6));
			const after = ba.getAttributeMap();

			expect(after).not.toBe(before);
			expect(after).toEqual([
				{ name: 'Strength', value: 10 },
				{ name: 'Agility', value: 6 }
			]);
		});

		it('resolves names from the Attributes reference set when available', () => {
			mockAttributes[EAttribute.Strength] = { id: EAttribute.Strength, name: 'Physical Power' } as IAttribute;
			const ba = new BattleAttributes(makeAttrs([EAttribute.Strength, 10]), false);

			expect(ba.getAttributeMap()).toEqual([{ name: 'Physical Power', value: 10 }]);
		});

		it('falls back to the normalised enum name when reference data is absent', () => {
			const ba = new BattleAttributes(makeAttrs([EAttribute.Strength, 10]), false);

			expect(ba.getAttributeMap()[0].name).toBe('Strength');
		});
	});

	describe('getAttributeCount', () => {
		it('returns the non-zero attribute count without building the full map', () => {
			const ba = new BattleAttributes(makeAttrs([EAttribute.Strength, 10], [EAttribute.Agility, 4]), false);
			expect(ba.getAttributeCount()).toBe(ba.getAttributeMap().length);
			expect(ba.getAttributeCount()).toBe(2);
		});

		it('reflects a modifier change', () => {
			const ba = new BattleAttributes([], false);
			expect(ba.getAttributeCount()).toBe(0);

			ba.addModifier(additive(EAttribute.Strength, 5));
			expect(ba.getAttributeCount()).toBe(1);
		});
	});

	// Mirrors Game.Core.Tests AttributeCollectionTests.RemoveModifier_* so the two attribute
	// implementations stay aligned on add/remove and the derived-cascade behaviour.
	describe('addModifier / removeModifier', () => {
		it('recomputes the value when a modifier is added', () => {
			const ba = new BattleAttributes([]);
			expect(ba.getValue(EAttribute.Strength)).toBe(0);

			ba.addModifier(additive(EAttribute.Strength, 10));
			expect(ba.getValue(EAttribute.Strength)).toBe(10);
		});

		it('reverts the value when a modifier is removed', () => {
			const ba = new BattleAttributes([]);
			const modifier = additive(EAttribute.Strength, 5);
			ba.addModifier(additive(EAttribute.Strength, 10));
			ba.addModifier(modifier);
			expect(ba.getValue(EAttribute.Strength)).toBe(15);

			expect(ba.removeModifier(modifier)).toBe(true);
			expect(ba.getValue(EAttribute.Strength)).toBe(10);
		});

		it('cascades a removal to derived attributes (Strength → MaxHealth)', () => {
			const ba = new BattleAttributes([]);
			const strength = additive(EAttribute.Strength, 10);
			ba.addModifier(strength);
			// MaxHealth derives from Strength (×5): 50 + 5*10 = 100.
			expect(ba.getValue(EAttribute.MaxHealth)).toBe(100);

			ba.removeModifier(strength);
			expect(ba.getValue(EAttribute.MaxHealth)).toBe(50);
		});

		it('restores the value when a removed modifier is re-added', () => {
			const ba = new BattleAttributes([]);
			const modifier = additive(EAttribute.Strength, 7);
			ba.addModifier(modifier);

			ba.removeModifier(modifier);
			expect(ba.getValue(EAttribute.Strength)).toBe(0);

			ba.addModifier(modifier);
			expect(ba.getValue(EAttribute.Strength)).toBe(7);
		});

		it('removes the exact instance among modifiers of the same type', () => {
			const ba = new BattleAttributes([]);
			const first = additive(EAttribute.Strength, 10);
			const second = additive(EAttribute.Strength, 3);
			ba.addModifier(first);
			ba.addModifier(second);

			ba.removeModifier(second);
			expect(ba.getValue(EAttribute.Strength)).toBe(10);
		});

		it('returns false when removing a modifier that was never added', () => {
			const ba = new BattleAttributes([]);
			ba.addModifier(additive(EAttribute.Strength, 10));

			expect(ba.removeModifier(additive(EAttribute.Strength, 10))).toBe(false);
			expect(ba.getValue(EAttribute.Strength)).toBe(10);
		});

		it('returns false when removing from an attribute that has no modifiers at all', () => {
			const ba = new BattleAttributes([]);
			// Intellect was never touched, so its bucket is absent — removal must be a safe no-op.
			expect(ba.removeModifier(additive(EAttribute.Intellect, 5))).toBe(false);
		});

		// A runtime-added Derived modifier registers a new cascade link, and removing it must unhook that
		// link only once the last modifier deriving from the source is gone (mirroring the backend's
		// AttributeCollection.UnhookDerivedLink so the source's invalidation cascade stays correct).
		it('hooks and unhooks a derived cascade link across add/remove', () => {
			const ba = new BattleAttributes([]);
			ba.addModifier(additive(EAttribute.Luck, 10)); // Luck = 10

			// Two Derived modifiers make Strength scale with Luck: Strength = (2 + 1) * Luck.
			const first = derived(EAttribute.Strength, 2, EAttribute.Luck);
			const second = derived(EAttribute.Strength, 1, EAttribute.Luck);
			ba.addModifier(first);
			ba.addModifier(second);
			expect(ba.getValue(EAttribute.Strength)).toBe(30);
			// The new Luck → Strength link also cascades into MaxHealth (+5*Strength).
			expect(ba.getValue(EAttribute.MaxHealth)).toBe(50 + 5 * 30);

			// Removing one leaves the other still deriving from Luck, so the link is kept and Luck cascades.
			expect(ba.removeModifier(first)).toBe(true);
			expect(ba.getValue(EAttribute.Strength)).toBe(10);
			ba.addModifier(additive(EAttribute.Luck, 10)); // Luck = 20
			expect(ba.getValue(EAttribute.Strength)).toBe(20);

			// Removing the last derived-from-Luck modifier unhooks the link: Luck no longer moves Strength.
			expect(ba.removeModifier(second)).toBe(true);
			expect(ba.getValue(EAttribute.Strength)).toBe(0);
			ba.addModifier(additive(EAttribute.Luck, 100));
			expect(ba.getValue(EAttribute.Strength)).toBe(0);
		});

		it('breaks a circular derived chain instead of recursing forever', () => {
			// Intellect ← Luck and Luck ← Intellect: the in-progress guard treats a re-entered attribute
			// as 0, so the incremental recompute terminates with both at 0 rather than overflowing.
			const ba = new BattleAttributes([]);
			ba.setData([], true, [
				derived(EAttribute.Intellect, 2, EAttribute.Luck),
				derived(EAttribute.Luck, 3, EAttribute.Intellect)
			]);
			expect(ba.getValue(EAttribute.Intellect)).toBe(0);
			expect(ba.getValue(EAttribute.Luck)).toBe(0);
		});
	});
});
