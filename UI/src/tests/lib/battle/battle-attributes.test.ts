import { describe, it, expect, beforeEach, vi } from 'vitest';
import { EAttribute, type IAttribute } from '$lib/api';
import { BattleAttributes } from '$lib/battle/battle-attributes';
import { EModifierType, EAttributeModifierSource, type AttributeModifier } from '$lib/battle/attribute-modifier';

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

	describe('derived stat computation', () => {
		it('calculates MaxHealth = 50 + 20*End + 5*Str', () => {
			const ba = new BattleAttributes(makeAttrs([EAttribute.Strength, 10], [EAttribute.Endurance, 20]));
			expect(ba.getValue(EAttribute.MaxHealth)).toBe(50 + 20 * 20 + 5 * 10);
		});

		it('calculates Defense = 2 + End + 0.5*Agi', () => {
			const ba = new BattleAttributes(makeAttrs([EAttribute.Endurance, 30], [EAttribute.Agility, 20]));
			expect(ba.getValue(EAttribute.Defense)).toBe(2 + 30 + 0.5 * 20);
		});

		it('calculates CooldownRecovery = 0.4*Agi + 0.1*Dex', () => {
			const ba = new BattleAttributes(makeAttrs([EAttribute.Agility, 20], [EAttribute.Dexterity, 10]));
			expect(ba.getValue(EAttribute.CooldownRecovery)).toBe(0.4 * 20 + 0.1 * 10);
		});

		it('handles zero base stats (MaxHealth still has base 50)', () => {
			const ba = new BattleAttributes([]);
			expect(ba.getValue(EAttribute.MaxHealth)).toBe(50);
			expect(ba.getValue(EAttribute.Defense)).toBe(2);
			expect(ba.getValue(EAttribute.CooldownRecovery)).toBe(0);
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
	});
});
