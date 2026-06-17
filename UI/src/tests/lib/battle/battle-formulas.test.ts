import { describe, it, expect } from 'vitest';
import { EAttribute } from '$lib/api';
import type { ISkill } from '$lib/api';
import { BattleAttributes } from '$lib/battle/battle-attributes';
import {
	applyDefense,
	calculateSkillDamage,
	cooldownMultiplier,
	skillContributions
} from '$lib/battle/battle-formulas';

const makeSkillData = (overrides: Partial<ISkill> = {}): ISkill => ({
	id: 1,
	name: 'Slash',
	baseDamage: 10,
	damageMultipliers: [],
	effects: [],
	description: 'A basic slash',
	cooldownMs: 1000,
	iconPath: '/icons/slash.png',
	...overrides
});

/** Raw attribute values without the derived/static pass, so tests control every input. */
const makeAttributes = (attrs: [EAttribute, number][] = []) =>
	new BattleAttributes(
		attrs.map(([attributeId, amount]) => ({ attributeId, amount })),
		false
	);

describe('battle-formulas', () => {
	describe('calculateSkillDamage', () => {
		it('returns baseDamage when there are no multipliers', () => {
			expect(calculateSkillDamage(makeSkillData({ baseDamage: 15 }), makeAttributes())).toBe(15);
		});

		it('adds attribute-scaled damage from a multiplier', () => {
			const skill = makeSkillData({
				baseDamage: 10,
				damageMultipliers: [{ attributeId: EAttribute.Strength, multiplier: 1.5 }]
			});
			const attributes = makeAttributes([[EAttribute.Strength, 20]]);

			expect(calculateSkillDamage(skill, attributes)).toBe(10 + 20 * 1.5);
		});

		it('sums multiple multipliers', () => {
			const skill = makeSkillData({
				baseDamage: 5,
				damageMultipliers: [
					{ attributeId: EAttribute.Strength, multiplier: 1.0 },
					{ attributeId: EAttribute.Agility, multiplier: 0.5 }
				]
			});
			const attributes = makeAttributes([
				[EAttribute.Strength, 10],
				[EAttribute.Agility, 20]
			]);

			expect(calculateSkillDamage(skill, attributes)).toBe(5 + 10 * 1.0 + 20 * 0.5);
		});

		it('contributes nothing for a zero attribute value', () => {
			const skill = makeSkillData({
				baseDamage: 10,
				damageMultipliers: [{ attributeId: EAttribute.Strength, multiplier: 2.0 }]
			});

			expect(calculateSkillDamage(skill, makeAttributes())).toBe(10);
		});
	});

	describe('skillContributions', () => {
		it('is empty for a skill with no multipliers', () => {
			expect(skillContributions(makeSkillData(), makeAttributes())).toEqual([]);
		});

		it('maps each multiplier to its attribute-scaled value', () => {
			const skill = makeSkillData({
				damageMultipliers: [
					{ attributeId: EAttribute.Strength, multiplier: 2 },
					{ attributeId: EAttribute.Luck, multiplier: 0.5 }
				]
			});
			const attributes = makeAttributes([
				[EAttribute.Strength, 20],
				[EAttribute.Luck, 8]
			]);

			expect(skillContributions(skill, attributes)).toEqual([
				{ attributeId: EAttribute.Strength, multiplier: 2, value: 40 },
				{ attributeId: EAttribute.Luck, multiplier: 0.5, value: 4 }
			]);
		});

		it('decomposes calculateSkillDamage: base plus the contributions equals the total', () => {
			const skill = makeSkillData({
				baseDamage: 7,
				damageMultipliers: [
					{ attributeId: EAttribute.Strength, multiplier: 1.25 },
					{ attributeId: EAttribute.Intellect, multiplier: 3 }
				]
			});
			const attributes = makeAttributes([
				[EAttribute.Strength, 13],
				[EAttribute.Intellect, 4]
			]);

			const sum = skillContributions(skill, attributes).reduce((total, c) => total + c.value, 0);
			expect(skill.baseDamage + sum).toBe(calculateSkillDamage(skill, attributes));
		});
	});

	describe('applyDefense', () => {
		it('subtracts flat defense from the raw damage', () => {
			expect(applyDefense(30, 10)).toBe(20);
		});

		it('passes raw damage through at zero defense', () => {
			expect(applyDefense(30, 0)).toBe(30);
		});

		it('clamps at zero when defense meets or exceeds the damage', () => {
			expect(applyDefense(10, 10)).toBe(0);
			expect(applyDefense(10, 40)).toBe(0);
		});

		it('subtracts block reduction alongside defense in the same clamp', () => {
			expect(applyDefense(30, 10, 5)).toBe(15); // 30 − 10 − 5
			expect(applyDefense(30, 10, 25)).toBe(0); // clamps when defense + block exceed the damage
		});
	});

	describe('cooldownMultiplier', () => {
		// CooldownRecovery is a base-1 multiplier read directly, so the attribute value IS the multiplier
		// (these raw attributes skip the static base; a real battler's base 1.0 is exercised in battler.test).
		it('reads the CooldownRecovery attribute directly as the multiplier', () => {
			expect(cooldownMultiplier(makeAttributes([[EAttribute.CooldownRecovery, 1.09]]))).toBeCloseTo(1.09, 10);
			expect(cooldownMultiplier(makeAttributes([[EAttribute.CooldownRecovery, 2]]))).toBe(2);
		});

		it('slows cooldowns for a CooldownRecovery below 1', () => {
			expect(cooldownMultiplier(makeAttributes([[EAttribute.CooldownRecovery, 0.5]]))).toBeCloseTo(0.5, 10);
		});
	});
});
