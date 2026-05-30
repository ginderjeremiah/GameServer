import { describe, it, expect } from 'vitest';
import { EAttribute } from '$lib/api';
import { BattleAttributes } from './battle-attributes';

const makeAttrs = (...pairs: [EAttribute, number][]) => pairs.map(([attributeId, amount]) => ({ attributeId, amount }));

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

	describe('calculateDerivedStats', () => {
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

		it('calculates DropBonus = log10(Luck)', () => {
			const ba = new BattleAttributes(makeAttrs([EAttribute.Luck, 100]));
			expect(ba.getValue(EAttribute.DropBonus)).toBeCloseTo(2, 10);
		});

		it('handles zero base stats (MaxHealth still has base 50)', () => {
			const ba = new BattleAttributes([]);
			expect(ba.getValue(EAttribute.MaxHealth)).toBe(50);
			expect(ba.getValue(EAttribute.Defense)).toBe(2);
			expect(ba.getValue(EAttribute.CooldownRecovery)).toBe(0);
		});

		it('DropBonus is -Infinity when Luck is 0 (log10(0))', () => {
			const ba = new BattleAttributes(makeAttrs([EAttribute.Luck, 0]));
			expect(ba.getValue(EAttribute.DropBonus)).toBe(-Infinity);
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
	});
});
