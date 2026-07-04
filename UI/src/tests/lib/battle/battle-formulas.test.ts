import { describe, it, expect } from 'vitest';
import { ERarity, EAttribute, EDamageType, ESkillAcquisition } from '$lib/api';
import type { ISkill } from '$lib/api';
import { BattleAttributes } from '$lib/battle/battle-attributes';
import {
	amplifiedDamage,
	toughnessMitigatedDamage,
	calculateSkillDamage,
	cooldownMultiplier,
	expectedCritMultiplier,
	mitigateDamage,
	resistanceTotal,
	skillContributions
} from '$lib/battle/battle-formulas';

const makeSkillData = (overrides: Partial<ISkill> = {}): ISkill => ({
	id: 1,
	name: 'Slash',
	baseDamage: 10,
	criticalChance: 0,
	damageMultipliers: [],
	effects: [],
	description: 'A basic slash',
	cooldownMs: 1000,
	damagePortions: [{ type: EDamageType.Physical, weight: 1 }],
	iconPath: '/icons/slash.png',
	rarityId: ERarity.Common,
	designerNotes: '',
	word: '',
	pronunciation: '',
	translation: '',
	acquisition: ESkillAcquisition.Player,
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

		// Per-hit-damage parity guard (mirrors BattleSkillTests.CalculateDamage_FloatGrouping_MatchesBackend).
		// Floating-point addition is not associative, so the backend's `base + (m1 + m2)` and a naive
		// `(base + m1) + m2` can differ by a ULP at a kill boundary — a live-vs-replay desync (#802). Two
		// contributions each below baseDamage's ULP vanish if added one at a time but survive when summed
		// first, so the correct grouping lifts the result above baseDamage and the wrong one returns it
		// unchanged — pinning the exact ordering, not just a coarse outcome.
		it('groups the multiplier sum like the backend (base added last) for ≥2 multipliers', () => {
			const tiny = 1e-16; // below the ULP of 1.0, so a single contribution is lost when added to base
			const skill = makeSkillData({
				baseDamage: 1,
				damageMultipliers: [
					{ attributeId: EAttribute.Strength, multiplier: tiny },
					{ attributeId: EAttribute.Agility, multiplier: tiny }
				]
			});
			const attributes = makeAttributes([
				[EAttribute.Strength, 1],
				[EAttribute.Agility, 1]
			]);

			const c1 = 1 * tiny;
			const c2 = 1 * tiny;
			const correct = skill.baseDamage + (c1 + c2); // base + (m1 + m2)
			const naive = skill.baseDamage + c1 + c2; // ((base + m1) + m2)
			// Sanity: the inputs are ULP-sensitive — the two groupings genuinely differ.
			expect(correct).not.toBe(naive);
			expect(naive).toBe(skill.baseDamage);

			expect(calculateSkillDamage(skill, attributes)).toBe(correct);
			expect(calculateSkillDamage(skill, attributes)).toBeGreaterThan(skill.baseDamage);
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

	// Toughness mitigation curve (#1330, constant denominator #1487): mirrors the backend
	// `Battler.ComputeNetDamage`. C = 200.
	describe('toughnessMitigatedDamage', () => {
		it('reduces by Toughness / (Toughness + C)', () => {
			// Toughness 200 (the curve's half-point) → 200/(200+200) = 0.5 reduction.
			expect(toughnessMitigatedDamage(40, 200)).toBeCloseTo(20, 10);
		});

		it('passes the hit through unchanged at zero Toughness', () => {
			expect(toughnessMitigatedDamage(40, 0)).toBe(40);
		});

		it('asymptotes below 100% — overwhelming Toughness still lets a sliver through', () => {
			// 5 × 200 / (2000 + 200) = 0.4545… (never zero, no immunity).
			expect(toughnessMitigatedDamage(5, 2000)).toBeCloseTo((5 * 200) / 2200, 10);
		});

		it('amplifies the hit for a negative Toughness within the pole (#1478)', () => {
			// A debuff-driven negative Toughness inverts the curve rather than flooring at 0% mitigation:
			// -100/(-100+200) = -1 reduction → 40 × (1 − (−1)) = 80.
			expect(toughnessMitigatedDamage(40, -100)).toBeCloseTo(80, 10);
		});
	});

	// Damage typing (#1320): mirrors the backend `Battler.AmplifyDamage` / `Battler.ComputeNetDamage` math.
	describe('amplifiedDamage', () => {
		it('leaves damage unchanged with no amplification (reduce-to-today identity)', () => {
			expect(amplifiedDamage(20, EDamageType.Physical, makeAttributes())).toBe(20);
		});

		it('sums the applicable amplification keys (fire + elemental)', () => {
			// applies(Fire) = { Fire, Elemental }: 0.3 + 0.2 = 0.5 → 40 × 1.5 = 60.
			const attrs = makeAttributes([
				[EAttribute.FireAmplification, 0.3],
				[EAttribute.ElementalAmplification, 0.2]
			]);
			expect(amplifiedDamage(40, EDamageType.Fire, attrs)).toBeCloseTo(60, 10);
		});

		it('ignores elemental amplification for a physical hit', () => {
			// applies(Physical) = { Physical } only — Physical is not a cross-cutting category.
			const attrs = makeAttributes([
				[EAttribute.PhysicalAmplification, 0.5],
				[EAttribute.ElementalAmplification, 1.0]
			]);
			expect(amplifiedDamage(20, EDamageType.Physical, attrs)).toBeCloseTo(30, 10);
		});
	});

	describe('mitigateDamage', () => {
		it('applies percentage resistance before the Toughness curve', () => {
			// FireResistance 0.5 halves 40 to 20, then Toughness 200 (the half-point) → 0.5 → 10.
			const attrs = makeAttributes([
				[EAttribute.Toughness, 200],
				[EAttribute.FireResistance, 0.5]
			]);
			expect(mitigateDamage(40, EDamageType.Fire, attrs)).toBeCloseTo(10, 10);
		});

		it('sums resistance across the applicable keys (fire + elemental)', () => {
			// 40 × 0.5 = 20; no Toughness leaves it unchanged.
			const attrs = makeAttributes([
				[EAttribute.FireResistance, 0.25],
				[EAttribute.ElementalResistance, 0.25]
			]);
			expect(mitigateDamage(40, EDamageType.Fire, attrs)).toBeCloseTo(20, 10);
		});

		it('treats negative resistance as vulnerability (unclamped)', () => {
			// 20 × 1.5 = 30; no Toughness leaves it unchanged.
			const attrs = makeAttributes([[EAttribute.FireResistance, -0.5]]);
			expect(mitigateDamage(20, EDamageType.Fire, attrs)).toBeCloseTo(30, 10);
		});

		it('returns a negative (healing) value on absorption and never applies the Toughness curve', () => {
			// FireResistance 2.0 → 20 × (1 − 2) = −20, with the curve NOT applied.
			const attrs = makeAttributes([
				[EAttribute.Toughness, 200],
				[EAttribute.FireResistance, 2.0]
			]);
			expect(mitigateDamage(20, EDamageType.Fire, attrs)).toBeCloseTo(-20, 10);
		});

		it('matches toughnessMitigatedDamage for a typed hit with no resistance', () => {
			const attrs = makeAttributes([[EAttribute.Toughness, 200]]);
			expect(mitigateDamage(50, EDamageType.Fire, attrs)).toBe(toughnessMitigatedDamage(50, 200));
		});
	});

	describe('resistanceTotal', () => {
		it('sums resistance across the applicable keys (fire + elemental)', () => {
			const attrs = makeAttributes([
				[EAttribute.FireResistance, 0.25],
				[EAttribute.ElementalResistance, 0.3]
			]);
			expect(resistanceTotal(EDamageType.Fire, attrs)).toBeCloseTo(0.55, 10);
		});

		it('is an exact 0 with no resistance authored', () => {
			expect(resistanceTotal(EDamageType.Physical, makeAttributes())).toBe(0);
		});

		it('agrees with the percentage mitigateDamage applies', () => {
			// The shared helper must report the same fraction the mitigation math used.
			const attrs = makeAttributes([[EAttribute.FireResistance, 0.4]]);
			expect(mitigateDamage(100, EDamageType.Fire, attrs)).toBeCloseTo(
				100 * (1 - resistanceTotal(EDamageType.Fire, attrs)),
				10
			);
		});
	});

	describe('cooldownMultiplier', () => {
		// Effective rate = CooldownRecovery + CooldownBonus × CooldownBonusMultiplier (#1426). These raw
		// attributes skip the static base, so CooldownBonus is 0 unless set (product 0); a real battler's base
		// 1.0 CDR and the Agility-derived multiplier are exercised in battler.test.
		it('reads CooldownRecovery directly as the base rate when no CooldownBonus is authored', () => {
			expect(cooldownMultiplier(makeAttributes([[EAttribute.CooldownRecovery, 1.09]]))).toBeCloseTo(1.09, 10);
			expect(cooldownMultiplier(makeAttributes([[EAttribute.CooldownRecovery, 2]]))).toBe(2);
		});

		it('adds the CooldownBonus × CooldownBonusMultiplier product to CooldownRecovery', () => {
			// 1 + 0.5 × 1.04 = 1.52 — the committed cadence channel composed at the charge site.
			const attrs = makeAttributes([
				[EAttribute.CooldownRecovery, 1],
				[EAttribute.CooldownBonus, 0.5],
				[EAttribute.CooldownBonusMultiplier, 1.04]
			]);
			expect(cooldownMultiplier(attrs)).toBeCloseTo(1.52, 10);
		});

		it('leaves the rate at CooldownRecovery when the bonus is zero (idle channel)', () => {
			// 0 × mult = 0, so a high multiplier with no enabler contributes nothing.
			const attrs = makeAttributes([
				[EAttribute.CooldownRecovery, 1],
				[EAttribute.CooldownBonusMultiplier, 2]
			]);
			expect(cooldownMultiplier(attrs)).toBeCloseTo(1, 10);
		});

		it('slows cooldowns for a CooldownRecovery below 1', () => {
			expect(cooldownMultiplier(makeAttributes([[EAttribute.CooldownRecovery, 0.5]]))).toBeCloseTo(0.5, 10);
		});
	});

	describe('expectedCritMultiplier', () => {
		it('is 1 when crit chance is 0', () => {
			expect(expectedCritMultiplier(0, 1.5)).toBe(1);
		});

		it('is 1 when crit damage is a 1x multiplier (a crit adds nothing)', () => {
			expect(expectedCritMultiplier(0.5, 1)).toBe(1);
		});

		it('blends the crit and non-crit damage by chance', () => {
			// 5% chance to deal 1.5x → expected 1 + 0.05 * 0.5 = 1.025
			expect(expectedCritMultiplier(0.05, 1.5)).toBeCloseTo(1.025, 10);
		});

		it('equals the crit multiplier at a guaranteed crit', () => {
			expect(expectedCritMultiplier(1, 2)).toBe(2);
		});
	});
});
