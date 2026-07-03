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
	criticalChance: 0,
	damageMultipliers: [],
	effects: [],
	description: '',
	cooldownMs,
	damagePortions: [{ type: EDamageType.Physical, weight: 1 }],
	iconPath: '',
	rarityId: ERarity.Common,
	designerNotes: '',
	word: '',
	pronunciation: '',
	translation: '',
	acquisition: ESkillAcquisition.Player
});

const makeTypedSkillData = (id: number, type: EDamageType): ISkill => ({
	...makeSkillData(id, 10, 1000),
	damagePortions: [{ type, weight: 1 }]
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

		// cdMultiplier = CooldownRecovery + CooldownBonus × CooldownBonusMultiplier (#1426). Mirrors the backend
		// BattlerTests GetCooldownMultiplier cases with the same scenarios and results.
		it('charges at the base rate regardless of Agility with no CooldownBonus enabler', () => {
			const battler = new Battler(
				makeBattlerData({
					attributes: [
						{ attributeId: EAttribute.Agility, amount: 50 },
						{ attributeId: EAttribute.Dexterity, amount: 20 }
					]
				})
			);

			// CDR is severed from the core attributes: with no authored CooldownBonus, Agility only lifts the
			// (idle) CooldownBonusMultiplier, so the effective rate is exactly the base-1 CooldownRecovery.
			expect(battler.cdMultiplier).toBeCloseTo(1, 10);
		});

		it('scales an authored CooldownBonus by the Agility-amplified CooldownBonusMultiplier', () => {
			const battler = new Battler(
				makeBattlerData({
					attributes: [
						{ attributeId: EAttribute.CooldownBonus, amount: 0.5 },
						{ attributeId: EAttribute.Agility, amount: 20 }
					]
				})
			);

			// CooldownBonus 0.5 × CooldownBonusMultiplier (1 + 0.002·AGI(20) = 1.04) on the base-1 CDR → 1.52.
			const expected = 1 + 0.5 * (1 + 0.002 * 20);
			expect(battler.cdMultiplier).toBeCloseTo(expected, 10);
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

		// The weapon-match gate (#1342), mirroring the backend BattleSnapshotTests gate cases. The punch
		// injection itself lives in InventoryManager (covered there); here the assembled grantedSkillIds —
		// punch included — are passed in directly, exactly as the battle engine does.
		describe('weapon-match gate (#1342)', () => {
			beforeEach(() => {
				mockSkills.length = 0;
				mockSkills[0] = makeTypedSkillData(0, EDamageType.Physical); // weapon-agnostic
				mockSkills[1] = makeTypedSkillData(1, EDamageType.Sword);
				mockSkills[2] = makeTypedSkillData(2, EDamageType.Axe);
				mockSkills[3] = makeTypedSkillData(3, EDamageType.Unarmed); // punch-typed
				mockSkills[4] = makeTypedSkillData(4, EDamageType.Fire); // weapon-agnostic
			});

			const fieldedIds = (battler: Battler) =>
				battler.skills.filter((s): s is Skill => s !== undefined).map((s) => s.id);

			it('fields every skill when ungated (no equipped weapon type — an enemy battler)', () => {
				const battler = new Battler(makeBattlerData({ selectedSkills: [1, 2, 0] }));
				expect(fieldedIds(battler)).toEqual([1, 2, 0]);
			});

			it('dims off-weapon selected skills and keeps the matching + agnostic ones', () => {
				const battler = new Battler(
					makeBattlerData({ selectedSkills: [1, 2, 4] }),
					undefined,
					undefined,
					undefined,
					EDamageType.Sword
				);
				// Sword(1) matches, Axe(2) dimmed, Fire(4) is weapon-agnostic.
				expect(fieldedIds(battler)).toEqual([1, 4]);
			});

			it('fields the Unarmed (punch-typed) skill bare-handed but dims a Sword skill', () => {
				const battler = new Battler(
					makeBattlerData({ selectedSkills: [3, 1] }),
					undefined,
					undefined,
					undefined,
					EDamageType.Unarmed
				);
				expect(fieldedIds(battler)).toEqual([3]);
			});

			it('gates granted skills uniformly (an off-weapon granted skill is dormant)', () => {
				// Selected Physical(0) kept; an Axe(2) granted while a Sword is wielded is dormant.
				const battler = new Battler(
					makeBattlerData({ selectedSkills: [0] }),
					undefined,
					[2],
					undefined,
					EDamageType.Sword
				);
				expect(fieldedIds(battler)).toEqual([0]);
			});

			it('still fields a matching granted signature when every selected skill is dimmed (no-stranding)', () => {
				// Selected Axe(2) is off-weapon (dimmed) while a Sword is wielded; the granted Sword(1) signature stays.
				const battler = new Battler(
					makeBattlerData({ selectedSkills: [2] }),
					undefined,
					[1],
					undefined,
					EDamageType.Sword
				);
				expect(fieldedIds(battler)).toEqual([1]);
			});

			it('skips a granted id that resolves to no skill (e.g. an unauthored punch)', () => {
				const battler = new Battler(
					makeBattlerData({ selectedSkills: [0] }),
					undefined,
					[99],
					undefined,
					EDamageType.Unarmed
				);
				expect(fieldedIds(battler)).toEqual([0]);
			});
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
		// ── Toughness mitigation curve (#1330, constant denominator #1487) ──
		// Mirrors the backend `BattlerTests` with the same scenarios and expected results. C = 200.

		it('applies the Toughness mitigation curve', () => {
			// Toughness = 2·Endurance(25) = 50 → 50/(50+200) = 0.2 reduction → 50 hit deals 40.
			const battler = new Battler(makeBattlerData({ attributes: [{ attributeId: EAttribute.Endurance, amount: 25 }] }));
			const initialHealth = battler.currentHealth;

			const finalDmg = battler.takeDamage(50, EDamageType.Physical);

			expect(finalDmg).toBeCloseTo(40, 10);
			expect(battler.currentHealth).toBeCloseTo(initialHealth - 40, 10);
		});

		it('mitigates exactly half at the constant (the half-point anchor)', () => {
			// Toughness = 2·Endurance(100) = 200 = C → exactly 0.5 reduction → 50 hit deals 25 (#1487).
			const battler = new Battler(
				makeBattlerData({ attributes: [{ attributeId: EAttribute.Endurance, amount: 100 }] })
			);

			expect(battler.takeDamage(50, EDamageType.Physical)).toBeCloseTo(25, 10);
		});

		it('leaves a hit unreduced with no Toughness', () => {
			// The reduce-to-nothing identity: no Endurance → Toughness 0 → 0 reduction.
			const battler = new Battler(makeBattlerData({ attributes: [] }));

			expect(battler.takeDamage(40, EDamageType.Physical)).toBeCloseTo(40, 10);
		});

		it('never fully mitigates a hit (asymptote below 100%)', () => {
			// Endurance 1000 → Toughness 2000; even so 5 × 200 / (2000 + 200) = 0.4545… > 0, never zero.
			const battler = new Battler(
				makeBattlerData({ attributes: [{ attributeId: EAttribute.Endurance, amount: 1000 }] })
			);

			const net = battler.takeDamage(5, EDamageType.Physical);
			expect(net).toBeGreaterThan(0);
			expect(net).toBeCloseTo((5 * 200) / 2200, 10);
		});

		it('has effective HP linear in Toughness', () => {
			// The EHP multiplier (raw/net = (Toughness + C)/C) rises by a constant step per Toughness point:
			// Toughness 0 → ×1, 200 → ×2, 400 → ×3 — equal +1 steps, so it is linear.
			const ehp0 = 40 / new Battler(makeBattlerData({ attributes: [] })).takeDamage(40, EDamageType.Physical);
			const ehp200 =
				40 /
				new Battler(makeBattlerData({ attributes: [{ attributeId: EAttribute.Endurance, amount: 100 }] })).takeDamage(
					40,
					EDamageType.Physical
				);
			const ehp400 =
				40 /
				new Battler(makeBattlerData({ attributes: [{ attributeId: EAttribute.Endurance, amount: 200 }] })).takeDamage(
					40,
					EDamageType.Physical
				);

			expect(ehp0).toBeCloseTo(1, 10);
			expect(ehp200).toBeCloseTo(2, 10);
			expect(ehp400).toBeCloseTo(3, 10);
			expect(ehp200 - ehp0).toBeCloseTo(ehp400 - ehp200, 10); // equal steps ⇒ linear
		});

		it('sets isDead when health drops to 0', () => {
			// No attributes → Toughness 0, so the hit lands in full.
			const battler = new Battler(makeBattlerData({ attributes: [] }));

			battler.takeDamage(battler.currentHealth, EDamageType.Physical);
			expect(battler.isDead).toBe(true);
		});

		it('sets isDead when health drops below 0', () => {
			const battler = new Battler(makeBattlerData({ attributes: [] }));

			battler.takeDamage(99999, EDamageType.Physical);
			expect(battler.isDead).toBe(true);
			expect(battler.currentHealth).toBeLessThan(0);
		});

		// ── Damage typing: percentage resistance then the Toughness curve (#1320 / #1330) ──
		// Mirrors the backend `BattlerTests` resistance cases with the same scenarios and expected results.

		it('applies percentage resistance before the Toughness curve', () => {
			// FireResistance 0.5 halves the 40 hit to 20, then Toughness 200 (the half-point → 0.5) → 10.
			const battler = new Battler(
				makeBattlerData({
					attributes: [
						{ attributeId: EAttribute.Endurance, amount: 100 },
						{ attributeId: EAttribute.FireResistance, amount: 0.5 }
					]
				})
			);

			expect(battler.takeDamage(40, EDamageType.Fire)).toBeCloseTo(10, 10);
		});

		it('sums resistance across the applicable keys (fire + elemental)', () => {
			// applies(Fire) = { Fire, Elemental }: 0.25 + 0.25 = 0.5 → 40 × 0.5 = 20; no Toughness leaves it at 20.
			const battler = new Battler(
				makeBattlerData({
					attributes: [
						{ attributeId: EAttribute.FireResistance, amount: 0.25 },
						{ attributeId: EAttribute.ElementalResistance, amount: 0.25 }
					]
				})
			);

			expect(battler.takeDamage(40, EDamageType.Fire)).toBeCloseTo(20, 10);
		});

		it('treats negative resistance as vulnerability (unclamped)', () => {
			// −0.5 FireResistance makes the target take 1.5× (20 × 1.5 = 30); no Toughness leaves it at 30.
			const battler = new Battler(
				makeBattlerData({ attributes: [{ attributeId: EAttribute.FireResistance, amount: -0.5 }] })
			);

			expect(battler.takeDamage(20, EDamageType.Fire)).toBeCloseTo(30, 10);
		});

		it('heals on absorption (resistance > 1) and never applies the Toughness curve', () => {
			// FireResistance 2.0 → 20 × (1 − 2) = −20: a net heal, with the curve NOT applied. Bring the battler
			// below MaxHealth first so the whole heal lands.
			const battler = new Battler(
				makeBattlerData({
					attributes: [
						{ attributeId: EAttribute.Endurance, amount: 0 },
						{ attributeId: EAttribute.FireResistance, amount: 2.0 }
					]
				})
			);
			battler.takeDamage(30, EDamageType.Physical); // 30 (no Toughness) → currentHealth 20

			const net = battler.takeDamage(20, EDamageType.Fire);

			expect(net).toBeCloseTo(-20, 10);
			expect(battler.currentHealth).toBeCloseTo(40, 10);
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
			battler.takeDamage(5, EDamageType.Physical); // 5 (no Toughness) → currentHealth 45

			const net = battler.takeDamage(20, EDamageType.Fire);

			expect(net).toBeCloseTo(-5, 10);
			expect(battler.currentHealth).toBeCloseTo(50, 10);
		});

		it('composes resistance then the Toughness curve for a typed hit', () => {
			// FireResistance 0.5 halves a 50 hit to 25, then Toughness 200 (the half-point → 0.5) → 12.5.
			const battler = new Battler(
				makeBattlerData({
					attributes: [
						{ attributeId: EAttribute.Endurance, amount: 100 },
						{ attributeId: EAttribute.FireResistance, amount: 0.5 }
					]
				})
			);

			expect(battler.takeDamage(50, EDamageType.Fire)).toBeCloseTo(12.5, 10);
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
