import { describe, it, expect, vi, beforeEach } from 'vitest';
import { ERarity, EAttribute, EDamageType, ESkillAcquisition, ESkillEffectTarget } from '$lib/api';
import type { ISkill } from '$lib/api';
import { EModifierType, EAttributeModifierSource } from '$lib/battle/attribute-modifier';
import { makeEffect } from './battle-sim-test-utils';

const mockSkills: ISkill[] = [];

vi.mock('$stores', () => ({
	staticData: {
		get skills() {
			return mockSkills;
		}
	}
}));

import { Battler } from '$lib/battle/battler';
import type { Skill } from '$lib/battle/skill';

const makeSkillData = (id: number, baseDamage: number, cooldownMs: number): ISkill => ({
	id,
	name: `Skill ${id}`,
	baseDamage,
	damageMultipliers: [],
	effects: [],
	description: '',
	cooldownMs,
	damageType: EDamageType.Physical,
	iconPath: '',
	rarityId: ERarity.Common,
	word: '',
	pronunciation: '',
	translation: '',
	acquisition: ESkillAcquisition.Player
});

const makeBattlerData = (overrides: Partial<Parameters<Battler['reset']>[0] & {}> = {}) => ({
	name: 'TestBattler',
	level: 5,
	selectedSkills: [0],
	attributes: [
		{ attributeId: EAttribute.Strength, amount: 10 },
		{ attributeId: EAttribute.Endurance, amount: 20 }
	],
	...overrides
});

describe('Battler', () => {
	beforeEach(() => {
		mockSkills.length = 0;
		mockSkills[0] = makeSkillData(0, 10, 1000);
		mockSkills[1] = makeSkillData(1, 20, 2000);
	});

	describe('reset', () => {
		it('sets name and level from battler data', () => {
			const battler = new Battler(makeBattlerData({ name: 'Hero', level: 10 }));

			expect(battler.name).toBe('Hero');
			expect(battler.level).toBe(10);
		});

		it('calculates currentHealth from derived MaxHealth', () => {
			const battler = new Battler(
				makeBattlerData({
					attributes: [
						{ attributeId: EAttribute.Strength, amount: 10 },
						{ attributeId: EAttribute.Endurance, amount: 20 }
					]
				})
			);

			const expectedMaxHealth = 50 + 20 * 20 + 5 * 10;
			expect(battler.currentHealth).toBe(expectedMaxHealth);
		});

		it('calculates cdMultiplier from CooldownRecovery (a base-1 multiplier read directly)', () => {
			const battler = new Battler(
				makeBattlerData({
					attributes: [
						{ attributeId: EAttribute.Agility, amount: 20 },
						{ attributeId: EAttribute.Dexterity, amount: 10 }
					]
				})
			);

			// CooldownRecovery = base 1 + 0.004·AGI + 0.001·DEX, read directly as the multiplier.
			const cdRecovery = 1 + 0.004 * 20 + 0.001 * 10;
			expect(battler.cdMultiplier).toBeCloseTo(cdRecovery, 10);
		});

		it('reads cdMultiplier live, reflecting a mid-battle CooldownRecovery change', () => {
			const battler = new Battler(makeBattlerData({ attributes: [] }));
			// No allocations, so CooldownRecovery is just the static base 1.0 → multiplier 1.0.
			expect(battler.cdMultiplier).toBe(1);

			// A +1.0 buff lands on the base 1.0, doubling the multiplier to 2.0.
			battler.attributes.addModifier({
				attribute: EAttribute.CooldownRecovery,
				amount: 1,
				type: EModifierType.Additive,
				source: EAttributeModifierSource.PlayerStatPoints
			});

			expect(battler.cdMultiplier).toBe(2);
		});

		it('sets isDead to false', () => {
			const battler = new Battler(makeBattlerData());
			expect(battler.isDead).toBe(false);
		});

		it('fills skill slots up to MAX_SELECTED_SKILLS', () => {
			const battler = new Battler(makeBattlerData({ selectedSkills: [0] }));
			expect(battler.skills).toHaveLength(4);
			expect(battler.skills[0]).toBeDefined();
			expect(battler.skills[1]).toBeUndefined();
		});

		it('merges additional attributes', () => {
			const additionalAttrs = [{ attributeId: EAttribute.Strength, amount: 5 }];
			const battler = new Battler(
				makeBattlerData({ attributes: [{ attributeId: EAttribute.Strength, amount: 10 }] }),
				additionalAttrs
			);

			expect(battler.attributes.getValue(EAttribute.Strength)).toBe(15);
		});

		// Proficiency bonuses (#982 area E / #1119) ride the modifier pipeline (by their additive/
		// multiplicative type), composing through computeAttributes exactly like the backend's snapshot.
		it('applies additional (proficiency) modifiers through the attribute pipeline', () => {
			const battler = new Battler(
				makeBattlerData({ attributes: [{ attributeId: EAttribute.Strength, amount: 10 }] }),
				undefined,
				undefined,
				[
					{
						attribute: EAttribute.Strength,
						amount: 5,
						type: EModifierType.Additive,
						source: EAttributeModifierSource.Proficiency
					},
					{
						attribute: EAttribute.Strength,
						amount: 2,
						type: EModifierType.Multiplicative,
						source: EAttributeModifierSource.Proficiency
					}
				]
			);

			// (10 alloc + 5 additive) * 2 multiplicative = 30, the additive-then-multiplicative order.
			expect(battler.attributes.getValue(EAttribute.Strength)).toBe(30);
		});

		it('reflects additional modifiers in derived MaxHealth (applied before health is read)', () => {
			const battler = new Battler(makeBattlerData({ attributes: [] }), undefined, undefined, [
				{
					attribute: EAttribute.Endurance,
					amount: 10,
					type: EModifierType.Additive,
					source: EAttributeModifierSource.Proficiency
				}
			]);

			// MaxHealth = 50 base + 20·Endurance(10) = 250, so the proficiency Endurance bonus is in currentHealth.
			expect(battler.currentHealth).toBe(250);
		});

		it('drops the additional modifiers on a data-less re-arm (setData replaced them; #811)', () => {
			const battler = new Battler(
				makeBattlerData({ attributes: [{ attributeId: EAttribute.Strength, amount: 10 }] }),
				undefined,
				undefined,
				[
					{
						attribute: EAttribute.Strength,
						amount: 5,
						type: EModifierType.Additive,
						source: EAttributeModifierSource.Proficiency
					}
				]
			);
			expect(battler.attributes.getValue(EAttribute.Strength)).toBe(15);

			// A data-less re-arm keeps the existing attribute set (proficiency modifier included) — the engine
			// re-derives with fresh modifiers only when they actually change.
			battler.reset();
			expect(battler.attributes.getValue(EAttribute.Strength)).toBe(15);
		});

		it('re-arms skill charges on a data-less reset so an unchanged re-spawn starts at zero charge (#811)', () => {
			const battler = new Battler(makeBattlerData({ selectedSkills: [0] }));
			battler.skills[0]!.chargeTime = 300;
			battler.skills[0]!.renderChargeTime = 300;

			// A data-less re-arm keeps the existing skills, so their charges must be reset here.
			battler.reset();

			expect(battler.skills[0]!.chargeTime).toBe(0);
			expect(battler.skills[0]!.renderChargeTime).toBe(0);
		});
	});

	describe('advanceCooldowns', () => {
		it('advances skill charge time by delta * cdMultiplier', () => {
			const battler = new Battler(
				makeBattlerData({
					attributes: [],
					selectedSkills: [0]
				})
			);

			battler.advanceCooldowns(500, () => {});
			expect(battler.skills[0]!.chargeTime).toBe(500 * battler.cdMultiplier);
		});

		it('invokes onFire for fired skills and resets their charge time', () => {
			const battler = new Battler(
				makeBattlerData({
					attributes: [],
					selectedSkills: [0]
				})
			);

			const fired: Skill[] = [];
			battler.advanceCooldowns(1000, (skill) => fired.push(skill));
			expect(fired).toHaveLength(1);
			expect(fired[0].name).toBe('Skill 0');
			expect(battler.skills[0]!.chargeTime).toBe(0);
		});

		it('does not invoke onFire when no skills are ready', () => {
			const battler = new Battler(
				makeBattlerData({
					attributes: [],
					selectedSkills: [0]
				})
			);

			const fired: Skill[] = [];
			battler.advanceCooldowns(100, (skill) => fired.push(skill));
			expect(fired).toHaveLength(0);
		});

		it('skips undefined skill slots', () => {
			const battler = new Battler(
				makeBattlerData({
					attributes: [],
					selectedSkills: [0]
				})
			);

			expect(() => battler.advanceCooldowns(500, () => {})).not.toThrow();
		});
	});

	describe('takeDamage', () => {
		it('reduces health by damage minus defense', () => {
			const battler = new Battler(
				makeBattlerData({
					attributes: [{ attributeId: EAttribute.Endurance, amount: 10 }]
				})
			);

			const defense = battler.attributes.getValue(EAttribute.Defense);
			const initialHealth = battler.currentHealth;
			const rawDamage = 50;

			const finalDmg = battler.takeDamage(rawDamage, EDamageType.Physical);

			expect(finalDmg).toBe(rawDamage - defense);
			expect(battler.currentHealth).toBe(initialHealth - finalDmg);
		});

		it('floors damage at 0 when defense exceeds raw damage', () => {
			const battler = new Battler(
				makeBattlerData({
					attributes: [{ attributeId: EAttribute.Endurance, amount: 100 }]
				})
			);

			const finalDmg = battler.takeDamage(5, EDamageType.Physical);
			expect(finalDmg).toBe(0);
			expect(battler.currentHealth).toBe(battler.attributes.getValue(EAttribute.MaxHealth));
		});

		it('sets isDead when health drops to 0', () => {
			const battler = new Battler(
				makeBattlerData({
					attributes: []
				})
			);

			battler.takeDamage(battler.currentHealth + battler.attributes.getValue(EAttribute.Defense), EDamageType.Physical);
			expect(battler.isDead).toBe(true);
		});

		it('sets isDead when health drops below 0', () => {
			const battler = new Battler(
				makeBattlerData({
					attributes: []
				})
			);

			battler.takeDamage(99999, EDamageType.Physical);
			expect(battler.isDead).toBe(true);
			expect(battler.currentHealth).toBeLessThan(0);
		});

		// ── Damage typing: percentage resistance then flat Defense (#1320) ──
		// Mirrors the backend `BattlerTests` resistance cases with the same scenarios and expected results.

		it('applies percentage resistance before flat defense', () => {
			// FireResistance 0.5 halves the 40-damage hit to 20, then flat Defense 12 → 8 net.
			const battler = new Battler(
				makeBattlerData({
					attributes: [
						{ attributeId: EAttribute.Endurance, amount: 10 },
						{ attributeId: EAttribute.FireResistance, amount: 0.5 }
					]
				})
			);

			expect(battler.takeDamage(40, EDamageType.Fire)).toBeCloseTo(8, 10);
		});

		it('sums resistance across the applicable keys (fire + elemental)', () => {
			// applies(Fire) = { Fire, Elemental }: 0.25 + 0.25 = 0.5 → 40 × 0.5 = 20, − 2 Defense = 18.
			const battler = new Battler(
				makeBattlerData({
					attributes: [
						{ attributeId: EAttribute.FireResistance, amount: 0.25 },
						{ attributeId: EAttribute.ElementalResistance, amount: 0.25 }
					]
				})
			);

			expect(battler.takeDamage(40, EDamageType.Fire)).toBeCloseTo(18, 10);
		});

		it('treats negative resistance as vulnerability (unclamped)', () => {
			// −0.5 FireResistance makes the target take 1.5× (20 × 1.5 = 30, − 2 Defense = 28).
			const battler = new Battler(
				makeBattlerData({ attributes: [{ attributeId: EAttribute.FireResistance, amount: -0.5 }] })
			);

			expect(battler.takeDamage(20, EDamageType.Fire)).toBeCloseTo(28, 10);
		});

		it('heals on absorption (resistance > 1) and never applies flat defense', () => {
			// FireResistance 2.0 → 20 × (1 − 2) = −20: a net heal, with flat Defense NOT subtracted. Bring the
			// battler below MaxHealth first so the whole heal lands.
			const battler = new Battler(
				makeBattlerData({
					attributes: [
						{ attributeId: EAttribute.Endurance, amount: 0 },
						{ attributeId: EAttribute.FireResistance, amount: 2.0 }
					]
				})
			);
			battler.takeDamage(27, EDamageType.Physical); // 27 − 2 Defense = 25 → currentHealth 25

			const net = battler.takeDamage(20, EDamageType.Fire);

			expect(net).toBeCloseTo(-20, 10);
			expect(battler.currentHealth).toBeCloseTo(45, 10);
		});

		it('caps the absorption heal at MaxHealth (no overheal)', () => {
			// Consistent with applyHealOverTime: only 5 of room remains, so a −20 absorption restores 5 and the
			// net reported is the capped −5, not −20.
			const battler = new Battler(
				makeBattlerData({
					attributes: [
						{ attributeId: EAttribute.Endurance, amount: 0 },
						{ attributeId: EAttribute.FireResistance, amount: 2.0 }
					]
				})
			);
			battler.takeDamage(7, EDamageType.Physical); // 7 − 2 Defense = 5 → currentHealth 45

			const net = battler.takeDamage(20, EDamageType.Fire);

			expect(net).toBeCloseTo(-5, 10);
			expect(battler.currentHealth).toBeCloseTo(50, 10);
		});

		it('is identical to the old flat step for a typed hit with no resistance', () => {
			// The reduce-to-today identity at the unit level: 50 − 12 Defense = 38, unchanged by typing.
			const battler = new Battler(makeBattlerData({ attributes: [{ attributeId: EAttribute.Endurance, amount: 10 }] }));

			expect(battler.takeDamage(50, EDamageType.Fire)).toBeCloseTo(38, 10);
		});
	});

	describe('applyEffect MaxHealth clamp', () => {
		// A MaxHealth debuff lowers health through the clamp, NOT a damage mutation (#1145). Deriving isDead
		// keeps it correct on this path by construction; a cached flag re-synced only at the damage mutations
		// would have missed it.
		it('reports isDead when a MaxHealth debuff clamps health to <= 0', () => {
			const battler = new Battler(
				makeBattlerData({ selectedSkills: [], attributes: [{ attributeId: EAttribute.Strength, amount: 10 }] })
			); // MaxHealth 100, currentHealth 100
			expect(battler.isDead).toBe(false);

			battler.applyEffect(
				makeEffect(0, ESkillEffectTarget.Opponent, EAttribute.MaxHealth, EModifierType.Additive, -200, 1000)
			);

			expect(battler.currentHealth).toBeLessThanOrEqual(0);
			expect(battler.isDead).toBe(true);
		});
	});

	describe('updateRenderCooldowns', () => {
		it('updates renderChargeTime without exceeding cooldownMs', () => {
			const battler = new Battler(
				makeBattlerData({
					attributes: [],
					selectedSkills: [0]
				})
			);

			battler.skills[0]!.chargeTime = 500;
			battler.updateRenderCooldowns(600);

			expect(battler.skills[0]!.renderChargeTime).toBeLessThanOrEqual(battler.skills[0]!.cooldownMs);
		});

		it('skips undefined skill slots', () => {
			const battler = new Battler(
				makeBattlerData({
					attributes: [],
					selectedSkills: [0]
				})
			);

			expect(() => battler.updateRenderCooldowns(100)).not.toThrow();
		});
	});
});
