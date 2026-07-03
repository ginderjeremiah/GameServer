import { describe, it, expect, beforeEach, vi } from 'vitest';
import { EDamageType, EAttribute, EModifierType, ESkillEffectTarget } from '$lib/api';
import type { ISkill } from '$lib/api';

const mockSkills: ISkill[] = [];
vi.mock('$stores', () => ({
	staticData: {
		get skills() {
			return mockSkills;
		}
	}
}));

import { battleStep, type BattleStepLog } from '$lib/battle';
import { Mulberry32 } from '$lib/engine/mulberry32';
import {
	battlerFactory,
	grantedBattlerFactory,
	makeSkill,
	makeMultiTypeSkill,
	makeEffect,
	type SkillSpec
} from './battle-sim-test-utils';

const makeBattler = battlerFactory(mockSkills);
const granted = grantedBattlerFactory(mockSkills);
// A battler carrying a resolved parry counter skill (#1457) — the id is registered and passed as the
// counterSkillId, exactly how the live engine threads InventoryManager.counterSkillId; the counter is
// deliberately not fielded as a loadout skill (mirroring the backend MakeBattlerWithCounter helper).
const makeParryBattler = (attrs: { id: EAttribute; amount: number }[], counterSpec: SkillSpec) =>
	granted.build(attrs, [], [], undefined, granted.register(counterSpec));
// A throwaway RNG for the steps that exercise no crit/dodge (their chances are 0, so the draws never
// change an outcome). The dedicated rolls + draw-order are covered by the crit/dodge block.
const noRng = () => new Mulberry32(0);

const newLog = (): BattleStepLog => ({
	appliedEffects: [],
	enemyDotDamage: 0,
	playerDotDamage: 0,
	enemyHotHeal: 0,
	playerHotHeal: 0
});
// A combatant with no Strength/Endurance has MaxHealth=50 and Toughness=0 (Toughness = 2·Endurance),
// so an incoming hit lands in full — the mitigation curve is exercised separately.
const baseStats = [{ id: EAttribute.Endurance, amount: 0 }];

describe('battleStep', () => {
	beforeEach(() => {
		mockSkills.length = 0;
	});

	it('fires the player’s ready skills and damages the enemy', () => {
		const player = makeBattler(baseStats, [makeSkill(30, 1000)]);
		const enemy = makeBattler(baseStats, []);
		const enemyHealth = enemy.currentHealth;

		const activations = battleStep(player, enemy, 1000, noRng());

		expect(activations).toHaveLength(1);
		expect(activations[0].byPlayer).toBe(true);
		expect(activations[0].damage).toBe(30); // 30 raw, enemy Toughness 0
		expect(enemy.currentHealth).toBe(enemyHealth - 30);
	});

	it('lets the enemy fire back when it survives the player’s hit', () => {
		const player = makeBattler(baseStats, [makeSkill(30, 1000)]);
		const enemy = makeBattler(baseStats, [makeSkill(20, 1000)]);
		const playerHealth = player.currentHealth;

		const activations = battleStep(player, enemy, 1000, noRng());

		expect(activations.map((a) => a.byPlayer)).toEqual([true, false]);
		expect(activations[1].damage).toBe(20); // 20 raw, player Toughness 0
		expect(player.currentHealth).toBe(playerHealth - 20);
	});

	it('mitigates but never fully blocks a hit, even against huge Toughness', () => {
		// The curve asymptotes below 100%: Toughness 1800 (Endurance 900) keeps
		// 200/(1800 + 200) = 10% of a 5-damage hit, so 0.5 trickles through — the old flat-Defense clamp to 0 is gone.
		const player = makeBattler(baseStats, [makeSkill(5, 1000)]);
		const enemy = makeBattler([{ id: EAttribute.Endurance, amount: 900 }], []);
		const enemyHealth = enemy.currentHealth;

		const activations = battleStep(player, enemy, 1000, noRng());

		expect(activations[0].damage).toBeCloseTo(0.5, 5);
		expect(enemy.currentHealth).toBeCloseTo(enemyHealth - 0.5, 5);
	});

	it('does not let a dead enemy counterattack in the same tick', () => {
		const player = makeBattler(baseStats, [makeSkill(100, 1000)]); // one-shots the 50-HP enemy
		const enemy = makeBattler(baseStats, [makeSkill(20, 1000)]);
		const playerHealth = player.currentHealth;

		const activations = battleStep(player, enemy, 1000, noRng());

		expect(enemy.isDead).toBe(true);
		expect(activations).toHaveLength(1);
		expect(activations[0].byPlayer).toBe(true);
		// The enemy never acted: its charge did not advance and the player took no damage.
		expect(enemy.skills[0]?.chargeTime).toBe(0);
		expect(player.currentHealth).toBe(playerHealth);
	});

	it('returns no activations when no skill is ready', () => {
		const player = makeBattler(baseStats, [makeSkill(30, 1000)]);
		const enemy = makeBattler(baseStats, [makeSkill(20, 1000)]);

		const activations = battleStep(player, enemy, 40, noRng());

		expect(activations).toHaveLength(0);
	});

	describe('log sink', () => {
		it('records a newly-applied effect and the side it landed on', () => {
			// The player's skill debuffs the enemy's Toughness (an Opponent-targeted effect).
			const player = makeBattler(baseStats, [
				makeSkill(
					0,
					1000,
					[],
					[makeEffect(1, ESkillEffectTarget.Opponent, EAttribute.Toughness, EModifierType.Additive, -5, 1000)]
				)
			]);
			const enemy = makeBattler(baseStats, []);
			const log = newLog();

			battleStep(player, enemy, 1000, noRng(), log);

			expect(log.appliedEffects).toHaveLength(1);
			expect(log.appliedEffects[0].effect.id).toBe(1);
			expect(log.appliedEffects[0].onPlayer).toBe(false); // it landed on the enemy
		});

		it('records each stacked application of an already-active effect', () => {
			const player = makeBattler(baseStats, [
				makeSkill(
					0,
					40,
					[],
					[makeEffect(1, ESkillEffectTarget.Self, EAttribute.Strength, EModifierType.Additive, 5, 1000)]
				)
			]);
			const enemy = makeBattler(baseStats, []);
			const log = newLog();

			battleStep(player, enemy, 40, noRng(), log); // fires → applies → recorded
			expect(log.appliedEffects).toHaveLength(1);
			expect(log.appliedEffects[0].onPlayer).toBe(true);

			// Firing again while still active now STACKS a new application, so it is recorded again (the
			// sink is reset each tick, so this tick's lone new application is the single entry).
			battleStep(player, enemy, 40, noRng(), log);
			expect(log.appliedEffects).toHaveLength(1);
			expect(log.appliedEffects[0].onPlayer).toBe(true);
			// The two applications fold into one view (one combined modifier) with a stack count of 2.
			expect(player.activeEffects).toHaveLength(1);
			expect(player.activeEffects[0].count).toBe(2);
		});

		it('reports per-tick DoT on one side and HoT on the other', () => {
			// Enemy bleeds 12/s; player regenerates 8/s — both authored straight onto the battler.
			const player = makeBattler([{ id: EAttribute.HealthRegenPerSecond, amount: 8 }, ...baseStats], []);
			const enemy = makeBattler([{ id: EAttribute.BleedDamagePerSecond, amount: 12 }, ...baseStats], []);
			player.currentHealth -= 20; // leave room so the regen is not capped away
			const log = newLog();

			battleStep(player, enemy, 1000, noRng(), log);

			expect(log.enemyDotDamage).toBe(12); // 12/s × 1000ms
			expect(log.playerHotHeal).toBe(8); // 8/s × 1000ms, room available
			expect(log.playerDotDamage).toBe(0);
			expect(log.enemyHotHeal).toBe(0);
		});

		it('resets the sink at the start of each tick', () => {
			const player = makeBattler(baseStats, []);
			const bleedingEnemy = makeBattler([{ id: EAttribute.BleedDamagePerSecond, amount: 12 }, ...baseStats], []);
			const cleanEnemy = makeBattler(baseStats, []);
			const log = newLog();

			battleStep(player, bleedingEnemy, 1000, noRng(), log);
			expect(log.enemyDotDamage).toBe(12);

			// Re-running with an enemy that has no DoT clears the previous value rather than accumulating.
			battleStep(player, cleanEnemy, 1000, noRng(), log);
			expect(log.enemyDotDamage).toBe(0);
		});
	});

	// End-of-tick ordering: a heal-over-time applies before the death check, so it can save a battler from an
	// otherwise-lethal DoT tick (#1090). Mirrors the backend BattleDamageOverTimeTests.
	describe('DoT/HoT ordering (heal before death check)', () => {
		it('lets a heal offset a lethal DoT tick and keep the player alive', () => {
			// 50-HP player takes 60 DoT this tick (more than its whole health) but an equal 60 heal applies
			// before the death check, restoring it to MaxHealth.
			const player = makeBattler(
				[
					{ id: EAttribute.BleedDamagePerSecond, amount: 1500 },
					{ id: EAttribute.HealthRegenPerSecond, amount: 1500 },
					...baseStats
				],
				[]
			);
			const enemy = makeBattler(baseStats, []);

			battleStep(player, enemy, 40, noRng());

			expect(player.isDead).toBe(false);
			expect(player.currentHealth).toBe(50); // 50 − 60 + 60, capped at MaxHealth
		});

		it('lets a heal offset a lethal DoT tick and keep the enemy alive', () => {
			// The enemy resolves first; its own heal applies before its death check too.
			const player = makeBattler(baseStats, []);
			const enemy = makeBattler(
				[
					{ id: EAttribute.BleedDamagePerSecond, amount: 1500 },
					{ id: EAttribute.HealthRegenPerSecond, amount: 1500 },
					...baseStats
				],
				[]
			);

			battleStep(player, enemy, 40, noRng());

			expect(enemy.isDead).toBe(false);
			expect(enemy.currentHealth).toBe(50);
		});

		it('still kills when the heal cannot offset the whole tick', () => {
			// A 5-HP player takes 10 DoT and heals 4, dying at −1: the heal applies but is insufficient.
			const player = makeBattler(
				[
					{ id: EAttribute.BleedDamagePerSecond, amount: 250 },
					{ id: EAttribute.HealthRegenPerSecond, amount: 100 },
					...baseStats
				],
				[]
			);
			player.takeDamage(45, EDamageType.Physical); // no Toughness → 45 damage → currentHealth 5
			const enemy = makeBattler(baseStats, []);
			const log = newLog();

			battleStep(player, enemy, 40, noRng(), log);

			expect(player.isDead).toBe(true);
			expect(player.currentHealth).toBe(-1); // 5 − 10 + 4
			expect(log.playerHotHeal).toBe(4);
		});
	});

	// Player-only crit/dodge + deterministic reflection, mirroring the backend BattleContextTests. Chances
	// are forced to 1/0 so the outcome is deterministic regardless of the seed; the draw-order test pins the
	// per-fire counts.
	describe('crit / dodge / reflection (player-only, seeded)', () => {
		it('multiplies a player crit by CriticalDamage before mitigation', () => {
			// CriticalDamage is the base 1.5 (sourced by #799) + 0.5 = 2, read directly as the multiplier.
			// The skill's own base chance (#1453) is 1, so it always crits (the multiplier stays at its base 1).
			const player = makeBattler(
				[{ id: EAttribute.CriticalDamage, amount: 0.5 }],
				[makeSkill(20, 40, [], [], undefined, 1)]
			);
			const enemy = makeBattler(baseStats, []);

			const activations = battleStep(player, enemy, 40, new Mulberry32(0));

			expect(activations[0].crit).toBe(true);
			expect(activations[0].damage).toBe(40); // 20×2, enemy Toughness 0
			expect(enemy.currentHealth).toBe(50 - 40);
		});

		it('deals raw damage when the player does not crit', () => {
			// The skill's own base chance defaults to 0 (#1453), so it never crits.
			const player = makeBattler(baseStats, [makeSkill(20, 40)]);
			const enemy = makeBattler(baseStats, []);

			const activations = battleStep(player, enemy, 40, new Mulberry32(0));

			expect(activations[0].crit).toBe(false);
			expect(activations[0].damage).toBe(20); // 20, enemy Toughness 0
		});

		it('zeroes an incoming hit the player dodges', () => {
			const player = makeBattler([{ id: EAttribute.DodgeChance, amount: 1 }], []);
			const enemy = makeBattler(baseStats, [makeSkill(20, 40)]);
			const before = player.currentHealth;

			const activations = battleStep(player, enemy, 40, new Mulberry32(0));

			expect(activations[0].byPlayer).toBe(false);
			expect(activations[0].dodged).toBe(true);
			expect(activations[0].damage).toBe(0);
			expect(player.currentHealth).toBe(before);
		});

		it('an uncommitted defender never dodges — the enemy hit lands unchanged (#1523)', () => {
			// DodgeChance is base 0 with no derivation (dodge rework #1523), mirroring ParryChance's
			// authored-only enabler.
			const player = makeBattler(baseStats, []);
			const enemy = makeBattler(baseStats, [makeSkill(20, 40)]);
			const before = player.currentHealth;

			const activations = battleStep(player, enemy, 40, new Mulberry32(0));

			expect(activations[0].dodged).toBe(false);
			expect(player.currentHealth).toBe(before - 20);
		});

		it('a multiplier alone never dodges at an authored chance of 0 (#1523)', () => {
			// The enabler is the authored DodgeChance, not the multiplier: base 1 + 999 = 1000 still
			// multiplies a 0 chance to 0 — the same commitment template as parry's authored-only enabler.
			const player = makeBattler([{ id: EAttribute.DodgeChanceMultiplier, amount: 999 }], []);
			const enemy = makeBattler(baseStats, [makeSkill(20, 40)]);
			const before = player.currentHealth;

			const activations = battleStep(player, enemy, 40, new Mulberry32(0));

			expect(activations[0].dodged).toBe(false);
			expect(player.currentHealth).toBe(before - 20);
		});

		it('the multiplier scales a fractional dodge chance to 1 — always dodges (#1523)', () => {
			// 0.5 authored × (base 1 + 1) = 1.0, at or above every [0,1) draw — mirroring how
			// ParryChanceMultiplier scales a fractional authored parry chance.
			const player = makeBattler(
				[
					{ id: EAttribute.DodgeChance, amount: 0.5 },
					{ id: EAttribute.DodgeChanceMultiplier, amount: 1 }
				],
				[]
			);
			const enemy = makeBattler(baseStats, [makeSkill(20, 40)]);
			const before = player.currentHealth;

			const activations = battleStep(player, enemy, 40, new Mulberry32(0));

			expect(activations[0].dodged).toBe(true);
			expect(activations[0].damage).toBe(0);
			expect(player.currentHealth).toBe(before);
		});

		it('reflects a share of a direct hit back to the attacker, bypassing its mitigation', () => {
			// The enemy carries 0.5 DamageReflection; the player's 40-damage hit lands in full (enemy Toughness
			// 0) and 40 × 0.5 = 20 is returned to the player, IGNORING the player's Toughness 100.
			const player = makeBattler([{ id: EAttribute.Endurance, amount: 50 }], [makeSkill(40, 40)]); // Toughness 100
			const enemy = makeBattler([{ id: EAttribute.DamageReflection, amount: 0.5 }], []);
			const playerBefore = player.currentHealth;

			const activations = battleStep(player, enemy, 40, new Mulberry32(0));

			expect(activations[0].damage).toBe(40);
			expect(activations[0].reflected).toBe(20); // surfaced for the combat log
			expect(player.currentHealth).toBe(playerBefore - 20); // 40 × 0.5, unmitigated
		});

		it('reflects an incoming hit back to the enemy', () => {
			// The player carries 0.4 DamageReflection; the enemy's 50-damage hit lands in full (player Toughness
			// 0) and 50 × 0.4 = 20 is dealt back to the enemy. The player (MaxHealth 150 from Strength) survives.
			const player = makeBattler(
				[
					{ id: EAttribute.Strength, amount: 20 },
					{ id: EAttribute.DamageReflection, amount: 0.4 }
				],
				[]
			);
			const enemy = makeBattler(baseStats, [makeSkill(50, 40)]);
			const enemyBefore = enemy.currentHealth;

			const activations = battleStep(player, enemy, 40, new Mulberry32(0));

			expect(activations[0].byPlayer).toBe(false);
			expect(activations[0].damage).toBe(50);
			expect(activations[0].reflected).toBe(20); // 50 × 0.4, surfaced for the combat log
			expect(enemy.currentHealth).toBe(enemyBefore - 20); // 50 × 0.4 reflected onto the enemy
		});

		it('reflects nothing when the incoming hit is dodged', () => {
			const player = makeBattler(
				[
					{ id: EAttribute.DodgeChance, amount: 1 },
					{ id: EAttribute.DamageReflection, amount: 1 }
				],
				[]
			);
			const enemy = makeBattler(baseStats, [makeSkill(50, 40)]);
			const enemyBefore = enemy.currentHealth;

			const activations = battleStep(player, enemy, 40, new Mulberry32(0));

			expect(activations[0].reflected).toBe(0); // a dodge zeroes the hit, so nothing reflects
			expect(enemy.currentHealth).toBe(enemyBefore); // a dodge zeroes the hit, so nothing reflects
		});

		it('reflects nothing when the incoming hit is absorbed', () => {
			// The enemy both reflects and absorbs Fire (resistance > 1 → net heal). The player's physical hit lands
			// and is reflected back (player 50 → 20), dropping the enemy to 20; the player's Fire hit is then
			// absorbed (net −20 heal), and the reflection guard returns on the non-positive net — so the player
			// takes no further damage. Mirrors the backend DamageTarget_AbsorbedHit_ReflectsNothing.
			const player = makeBattler(baseStats, [
				makeSkill(30, 40, [], [], EDamageType.Physical),
				makeSkill(20, 40, [], [], EDamageType.Fire)
			]);
			const enemy = makeBattler(
				[
					{ id: EAttribute.FireResistance, amount: 2 },
					{ id: EAttribute.DamageReflection, amount: 1 }
				],
				[]
			);

			battleStep(player, enemy, 40, new Mulberry32(0));

			// Player took only the reflected physical 30 (50 → 20); the absorbed Fire hit reflected nothing.
			expect(player.currentHealth).toBe(20);
		});

		// Mirrors the backend's BattleContextTests composition scenarios for skill.criticalChance ×
		// CriticalChanceMultiplier inside the crit roll (#1464, surfaced during review of #1458).
		describe('CriticalChanceMultiplier composition (#1453)', () => {
			it('never crits even with a heavy multiplier investment when the skill has zero base critical chance', () => {
				// The enabler is the SKILL's own base chance, not the multiplier: CriticalChanceMultiplier's base 1 +
				// 999 = 1000 still crits for nothing when the fired skill's own criticalChance is 0 (0 × 1000 = 0).
				const player = makeBattler(
					[{ id: EAttribute.CriticalChanceMultiplier, amount: 999 }],
					[makeSkill(20, 40)] // criticalChance defaults to 0
				);
				const enemy = makeBattler(baseStats, []);

				const activations = battleStep(player, enemy, 40, new Mulberry32(0));

				expect(activations[0].crit).toBe(false);
				expect(activations[0].damage).toBe(20); // enemy Toughness 0
				expect(enemy.currentHealth).toBe(50 - 20);
			});

			it('composes multiplicatively with the skill’s base, letting a zeroed multiplier cancel an always-crit skill', () => {
				// CriticalChanceMultiplier's base 1 + (-1) = 0 cancels even a skill authored to always crit on its
				// own (criticalChance 1): 1 × 0 = 0, proving the composition is a product, not an independent OR.
				const player = makeBattler(
					[{ id: EAttribute.CriticalChanceMultiplier, amount: -1 }],
					[makeSkill(20, 40, [], [], undefined, 1)]
				);
				const enemy = makeBattler(baseStats, []);

				const activations = battleStep(player, enemy, 40, new Mulberry32(0));

				expect(activations[0].crit).toBe(false);
				expect(activations[0].damage).toBe(20);
				expect(enemy.currentHealth).toBe(50 - 20);
			});

			it('scales a fractional skill base above one and always crits', () => {
				// A fractional skill base (0.5) scaled by a CriticalChanceMultiplier of 2 (base 1 + 1 = 2) reaches
				// an effective chance of 1.0 — at or above every possible [0,1) RNG draw — even though neither
				// factor alone would guarantee it. CriticalDamage is the base 1.5 + 0.5 = 2.
				const player = makeBattler(
					[
						{ id: EAttribute.CriticalChanceMultiplier, amount: 1 },
						{ id: EAttribute.CriticalDamage, amount: 0.5 }
					],
					[makeSkill(20, 40, [], [], undefined, 0.5)]
				);
				const enemy = makeBattler(baseStats, []);

				const activations = battleStep(player, enemy, 40, new Mulberry32(0));

				expect(activations[0].crit).toBe(true);
				expect(activations[0].damage).toBe(40); // 20 × 2 (CriticalDamage), no Toughness
				expect(enemy.currentHealth).toBe(50 - 40);
			});
		});

		it('never crits on the enemy’s attack, even with a forced crit chance', () => {
			const player = makeBattler(baseStats, []);
			const enemy = makeBattler(
				[{ id: EAttribute.CriticalDamage, amount: 2 }],
				[makeSkill(20, 40, [], [], undefined, 1)]
			);

			const activations = battleStep(player, enemy, 40, new Mulberry32(0));

			expect(activations[0].crit).toBe(false);
			expect(activations[0].damage).toBe(20); // 20 (no crit), NOT 40
		});

		describe('Cull execute overlay (#1430)', () => {
			it('deals unmultiplied damage without an authored ExecuteBonus', () => {
				const player = makeBattler(baseStats, [makeSkill(10, 40)]);
				const enemy = makeBattler(baseStats, []); // MaxHealth 50, Toughness 0
				enemy.currentHealth = 30; // 40% missing, but nothing to scale the multiplier

				const activations = battleStep(player, enemy, 40, new Mulberry32(0));

				expect(activations[0].damage).toBe(10);
				expect(enemy.currentHealth).toBe(30 - 10);
			});

			it('deals unmultiplied damage against a target at full health', () => {
				const player = makeBattler([{ id: EAttribute.ExecuteBonus, amount: 1 }], [makeSkill(10, 40)]);
				const enemy = makeBattler(baseStats, []); // full health ⇒ missing-HP fraction 0

				const activations = battleStep(player, enemy, 40, new Mulberry32(0));

				expect(activations[0].damage).toBe(10);
			});

			it('scales raw damage by 1 + ExecuteBonus × the target’s missing-health fraction', () => {
				// The enemy is missing 40% of its health (20/50), so a full (100%) ExecuteBonus scales this fire's
				// multiplier to 1 + 1.0×0.4 = 1.4: 10 raw × 1.4 = 14 dealt (vs 10 unmultiplied).
				const player = makeBattler([{ id: EAttribute.ExecuteBonus, amount: 1 }], [makeSkill(10, 40)]);
				const enemy = makeBattler(baseStats, []); // MaxHealth 50, Toughness 0
				enemy.currentHealth = 30; // 40% missing

				const activations = battleStep(player, enemy, 40, new Mulberry32(0));

				expect(activations[0].damage).toBe(14);
				expect(enemy.currentHealth).toBe(30 - 14);
			});

			it('never applies to the enemy’s attack, even with a forced ExecuteBonus', () => {
				const player = makeBattler(baseStats, []); // MaxHealth 50, Toughness 0
				player.currentHealth = 30; // 40% missing, so the enemy's ExecuteBonus WOULD apply if it were live
				const enemy = makeBattler([{ id: EAttribute.ExecuteBonus, amount: 1 }], [makeSkill(10, 40)]);

				const activations = battleStep(player, enemy, 40, new Mulberry32(0));

				expect(activations[0].byPlayer).toBe(false);
				expect(activations[0].damage).toBe(10); // 10 (unmultiplied), NOT 14
			});
		});

		it('draws once per player fire and thrice per enemy fire, in order', () => {
			// One crit draw for the player's hit, then three draws for the enemy's — parry, dodge, and the parry
			// counter's crit (#1457; Block's former second draw was retired in #1330) — four draws total, all
			// taken unconditionally so the stream is a pure function of the fire sequence.
			const seed = 12345;
			const rng = new Mulberry32(seed);
			const player = makeBattler(baseStats, [makeSkill(10, 40)]);
			const enemy = makeBattler([{ id: EAttribute.Endurance, amount: 100 }], [makeSkill(10, 40)]); // tanky, survives

			battleStep(player, enemy, 40, rng);

			const reference = new Mulberry32(seed);
			for (let i = 0; i < 4; i++) {
				reference.next();
			}
			expect(rng.next()).toBe(reference.next());
		});
	});

	// Parry / riposte (#1457): an incoming enemy hit can be parried — fully negated, answered with the
	// counter skill (the equipped weapon's signature) fired as a first-class player hit. Mirrors the
	// backend BattleContextTests parry block scenario-for-scenario (the health/activation-observable
	// subset — the stats bookings are backend-only).
	describe('parry / riposte (#1457)', () => {
		it('an uncommitted defender never parries — the enemy hit lands unchanged', () => {
			const player = makeBattler(baseStats, []);
			const enemy = makeBattler(baseStats, [makeSkill(20, 40)]);
			const before = player.currentHealth;

			const activations = battleStep(player, enemy, 40, noRng());

			expect(activations).toHaveLength(1);
			expect(activations[0].parried).toBe(false);
			expect(player.currentHealth).toBe(before - 20);
		});

		it('a multiplier alone never parries at an authored chance of 0', () => {
			// The enabler is the authored ParryChance, not the multiplier: base 1 + 999 = 1000 still
			// multiplies a 0 chance to 0 — the same commitment template as crit's per-skill enabler.
			const player = makeBattler([{ id: EAttribute.ParryChanceMultiplier, amount: 999 }], []);
			const enemy = makeBattler(baseStats, [makeSkill(20, 40)]);
			const before = player.currentHealth;

			const activations = battleStep(player, enemy, 40, noRng());

			expect(activations[0].parried).toBe(false);
			expect(player.currentHealth).toBe(before - 20);
		});

		it('the multiplier scales a fractional chance to 1 — always parries', () => {
			// 0.5 authored × (base 1 + 1) = 1.0, at or above every [0,1) draw — mirroring how
			// CriticalChanceMultiplier scales a skill's fractional base.
			const player = makeBattler(
				[
					{ id: EAttribute.ParryChance, amount: 0.5 },
					{ id: EAttribute.ParryChanceMultiplier, amount: 1 }
				],
				[]
			);
			const enemy = makeBattler(baseStats, [makeSkill(20, 40)]);
			const before = player.currentHealth;

			const activations = battleStep(player, enemy, 40, noRng());

			expect(activations[0].parried).toBe(true);
			expect(activations[0].damage).toBe(0);
			expect(player.currentHealth).toBe(before);
		});

		it('a parry without a resolvable counter skill negates without a riposte', () => {
			const player = makeBattler([{ id: EAttribute.ParryChance, amount: 1 }], []);
			const enemy = makeBattler(baseStats, [makeSkill(30, 40)]);
			const playerBefore = player.currentHealth;
			const enemyBefore = enemy.currentHealth;

			const activations = battleStep(player, enemy, 40, noRng());

			expect(activations).toHaveLength(1); // no counter activation follows
			expect(activations[0].parried).toBe(true);
			expect(player.currentHealth).toBe(playerBefore);
			expect(enemy.currentHealth).toBe(enemyBefore);
		});

		it('parry takes precedence over dodge', () => {
			// Dodge investment can never starve the riposte — no anti-synergy between the defensive layers.
			const player = makeBattler(
				[
					{ id: EAttribute.ParryChance, amount: 1 },
					{ id: EAttribute.DodgeChance, amount: 1 }
				],
				[]
			);
			const enemy = makeBattler(baseStats, [makeSkill(20, 40)]);

			const activations = battleStep(player, enemy, 40, noRng());

			expect(activations[0].parried).toBe(true);
			expect(activations[0].dodged).toBe(false);
		});

		it('a proc’d parry consumes the same three draws as a quiet enemy fire', () => {
			// The counter's crit decision uses the already-taken third draw rather than drawing again, so
			// the stream advances identically whether or not the parry procs.
			const seed = 12345;
			const rng = new Mulberry32(seed);
			const player = makeParryBattler([{ id: EAttribute.ParryChance, amount: 1 }], makeSkill(10, 100_000));
			const enemy = makeBattler([{ id: EAttribute.Endurance, amount: 100 }], [makeSkill(10, 40)]);

			battleStep(player, enemy, 40, rng);

			const reference = new Mulberry32(seed);
			for (let i = 0; i < 3; i++) {
				reference.next();
			}
			expect(rng.next()).toBe(reference.next());
		});

		it('the riposte fires the weapon signature at the enemy as a first-class player hit', () => {
			// raw = BaseDamage + STR × mult (10 + 10 × 0.5 = 15), typed by the signature (Sword), pushed as
			// a byPlayer counter activation after the parried enemy activation.
			const counter = makeSkill(
				10,
				100_000,
				[{ attributeId: EAttribute.Strength, multiplier: 0.5 }],
				[],
				EDamageType.Sword
			);
			const player = makeParryBattler(
				[
					{ id: EAttribute.ParryChance, amount: 1 },
					{ id: EAttribute.Strength, amount: 10 }
				],
				counter
			);
			const enemy = makeBattler(baseStats, [makeSkill(20, 40)]);
			const playerBefore = player.currentHealth;

			const activations = battleStep(player, enemy, 40, noRng());

			expect(activations).toHaveLength(2);
			expect(activations[0].parried).toBe(true);
			expect(activations[1].byPlayer).toBe(true);
			expect(activations[1].counter).toBe(true);
			expect(activations[1].damage).toBe(15);
			expect(enemy.currentHealth).toBe(50 - 15);
			expect(player.currentHealth).toBe(playerBefore);
		});

		it('the riposte is amplified by the defender’s own offense', () => {
			// Unlike reflection (scaled by the incoming hit, bypassing mitigation), the counter is the
			// defender's own attack: its typed amplification applies (Sword ⇒ Sword + Physical keys) —
			// 10 × (1 + 0.2 + 0.1) = 13.
			const counter = makeSkill(10, 100_000, [], [], EDamageType.Sword);
			const player = makeParryBattler(
				[
					{ id: EAttribute.ParryChance, amount: 1 },
					{ id: EAttribute.SwordAmplification, amount: 0.2 },
					{ id: EAttribute.PhysicalAmplification, amount: 0.1 }
				],
				counter
			);
			const enemy = makeBattler(baseStats, [makeSkill(20, 40)]);

			const activations = battleStep(player, enemy, 40, noRng());

			expect(activations[1].damage).toBeCloseTo(13, 10);
		});

		it('the riposte is mitigated by the enemy', () => {
			// The counter runs the normal direct-hit pipeline, so the enemy's Toughness curve applies
			// (Toughness 200 vs the 200 constant ⇒ 50%): a genuine attack, not thorns.
			const counter = makeSkill(30, 100_000, [], [], EDamageType.Sword);
			const player = makeParryBattler([{ id: EAttribute.ParryChance, amount: 1 }], counter);
			const enemy = makeBattler([{ id: EAttribute.Endurance, amount: 100 }], [makeSkill(20, 40)]);

			const activations = battleStep(player, enemy, 40, noRng());

			expect(activations[1].damage).toBe(15);
		});

		it('the riposte crits from its authored chance', () => {
			// The signature's own authored CriticalChance (1) × CriticalChanceMultiplier, multiplied by
			// CriticalDamage (1.5 + 0.5 = 2) — the standard opt-in template.
			const counter = makeSkill(10, 100_000, [], [], EDamageType.Sword, 1);
			const player = makeParryBattler(
				[
					{ id: EAttribute.ParryChance, amount: 1 },
					{ id: EAttribute.CriticalDamage, amount: 0.5 }
				],
				counter
			);
			const enemy = makeBattler(baseStats, [makeSkill(20, 40)]);

			const activations = battleStep(player, enemy, 40, noRng());

			expect(activations[1].crit).toBe(true);
			expect(activations[1].damage).toBe(20);
		});

		it('the riposte never crits without an authored chance', () => {
			const counter = makeSkill(10, 100_000, [], [], EDamageType.Sword);
			const player = makeParryBattler(
				[
					{ id: EAttribute.ParryChance, amount: 1 },
					{ id: EAttribute.CriticalChanceMultiplier, amount: 999 }
				],
				counter
			);
			const enemy = makeBattler(baseStats, [makeSkill(20, 40)]);

			const activations = battleStep(player, enemy, 40, noRng());

			expect(activations[1].crit).toBe(false);
			expect(activations[1].damage).toBe(10);
		});

		it('the riposte triggers the enemy’s reflection', () => {
			// The counter is a genuine direct hit the enemy takes, so an enemy with authored DamageReflection
			// returns its share of the counter to the player, bypassing the player's mitigation.
			const counter = makeSkill(10, 100_000, [], [], EDamageType.Sword);
			const player = makeParryBattler(
				[
					{ id: EAttribute.ParryChance, amount: 1 },
					{ id: EAttribute.Endurance, amount: 10 }
				],
				counter
			);
			const enemy = makeBattler([{ id: EAttribute.DamageReflection, amount: 0.5 }], [makeSkill(20, 40)]);
			const playerBefore = player.currentHealth;

			const activations = battleStep(player, enemy, 40, noRng());

			expect(activations[1].reflected).toBe(5);
			expect(player.currentHealth).toBe(playerBefore - 5);
		});

		it('the riposte can kill the enemy', () => {
			const counter = makeSkill(60, 100_000, [], [], EDamageType.Sword);
			const player = makeParryBattler([{ id: EAttribute.ParryChance, amount: 1 }], counter);
			const enemy = makeBattler(baseStats, [makeSkill(20, 40)]); // MaxHealth 50

			battleStep(player, enemy, 40, noRng());

			expect(enemy.isDead).toBe(true);
		});

		it('a player attack is never parried — parry is player-only', () => {
			// Even with ParryChance 1 and its own counter skill, the enemy takes the player's hit un-parried,
			// gated on who acts like crit/dodge.
			const player = makeBattler(baseStats, [makeSkill(20, 40)]);
			const enemy = makeParryBattler([{ id: EAttribute.ParryChance, amount: 1 }], makeSkill(10, 100_000));

			const activations = battleStep(player, enemy, 40, noRng());

			expect(activations).toHaveLength(1);
			expect(activations[0].parried).toBe(false);
			expect(enemy.currentHealth).toBe(50 - 20);
		});

		it('damage-over-time is never parried', () => {
			// DoT has no coherent attacker for a tick, so it bypasses the direct-hit pipeline entirely —
			// mirroring dodge and reflection.
			const player = makeParryBattler(
				[
					{ id: EAttribute.ParryChance, amount: 1 },
					{ id: EAttribute.BleedDamagePerSecond, amount: 10 }
				],
				makeSkill(10, 100_000)
			);
			const enemy = makeBattler(baseStats, []);
			const before = player.currentHealth;
			const enemyBefore = enemy.currentHealth;

			const activations = battleStep(player, enemy, 1000, noRng());

			expect(activations).toHaveLength(0);
			expect(player.currentHealth).toBe(before - 10);
			expect(enemy.currentHealth).toBe(enemyBefore);
		});
	});

	// Multi-typed direct hits: the raw hit splits across weighted portions, each run through the single-type
	// pipeline and summed. Mirrors the backend BattleContextTests multi-portion block.
	describe('multi-typed portion loop (#1343)', () => {
		it('splits raw by weight and sums each portion’s net', () => {
			// [Physical 60, Fire 40] of a raw-100 hit → 60 + 40 = 100 split; the enemy resists Fire 0.5 (Physical
			// unresisted), no Toughness: 60 + 40×0.5 = 80. The enemy (Str 30 → MaxHealth 200) drops to 120.
			const player = makeBattler(baseStats, [
				makeMultiTypeSkill(100, 40, [
					{ type: EDamageType.Physical, weight: 60 },
					{ type: EDamageType.Fire, weight: 40 }
				])
			]);
			const enemy = makeBattler(
				[
					{ id: EAttribute.Strength, amount: 30 },
					{ id: EAttribute.FireResistance, amount: 0.5 }
				],
				[]
			);

			const activations = battleStep(player, enemy, 40, noRng());

			expect(activations[0].damage).toBe(80);
			expect(enemy.currentHealth).toBe(200 - 80);
		});

		it('multiplies every portion by a single crit', () => {
			// CriticalDamage 1.5 + 0.5 = 2. The skill's own base chance (#1453) is 1, so it always crits, scaling
			// BOTH portions of [Physical 50, Fire 50] of raw 20 → 10 each, ×2 → 20 each = 40 (a per-portion-only
			// crit would give 30).
			const player = makeBattler(
				[{ id: EAttribute.CriticalDamage, amount: 0.5 }],
				[
					makeMultiTypeSkill(
						20,
						40,
						[
							{ type: EDamageType.Physical, weight: 50 },
							{ type: EDamageType.Fire, weight: 50 }
						],
						[],
						[],
						1
					)
				]
			);
			const enemy = makeBattler([{ id: EAttribute.Strength, amount: 10 }], []);

			const activations = battleStep(player, enemy, 40, new Mulberry32(0));

			expect(activations[0].crit).toBe(true);
			expect(activations[0].damage).toBe(40);
			expect(enemy.currentHealth).toBe(100 - 40);
		});

		it('zeroes the whole multi-typed hit on a single dodge', () => {
			// The enemy's [Physical 50, Fire 50] split of a 1000-damage hit is fully dodged — zeroing BOTH
			// portions — so the 50-HP player takes nothing (one un-dodged portion would kill it).
			const player = makeBattler([{ id: EAttribute.DodgeChance, amount: 1 }], []);
			const enemy = makeBattler(baseStats, [
				makeMultiTypeSkill(1000, 40, [
					{ type: EDamageType.Physical, weight: 50 },
					{ type: EDamageType.Fire, weight: 50 }
				])
			]);
			const before = player.currentHealth;

			const activations = battleStep(player, enemy, 40, new Mulberry32(0));

			expect(activations[0].byPlayer).toBe(false);
			expect(activations[0].dodged).toBe(true);
			expect(activations[0].damage).toBe(0);
			expect(player.currentHealth).toBe(before);
		});

		it('applies per-portion vulnerability to only the matching portion', () => {
			// [Physical 50, Fire 50] of raw 40 → 20 each; the enemy's −1.0 FireResistance doubles only the Fire
			// portion (20 → 40), Physical stays 20: 60 total. The enemy (Str 26 → MaxHealth 180) drops to 120.
			const player = makeBattler(baseStats, [
				makeMultiTypeSkill(40, 40, [
					{ type: EDamageType.Physical, weight: 50 },
					{ type: EDamageType.Fire, weight: 50 }
				])
			]);
			const enemy = makeBattler(
				[
					{ id: EAttribute.Strength, amount: 26 },
					{ id: EAttribute.FireResistance, amount: -1.0 }
				],
				[]
			);

			const activations = battleStep(player, enemy, 40, noRng());

			expect(activations[0].damage).toBe(60);
			expect(enemy.currentHealth).toBe(180 - 60);
		});

		it('caps an absorbing portion’s heal at the room opened by the earlier portion', () => {
			// [Physical 20, Fire 80] of raw 100 against an enemy absorbing Fire (FireResistance 2.0) at full HP:
			// Physical deals 20 (100 → 80, opening 20 room), then the Fire portion's −80 absorption heal is capped
			// at that 20 room (back to 100). Net 0 — the fixed Physical-first order is what lets the heal land.
			const player = makeBattler(
				[],
				[
					makeMultiTypeSkill(100, 40, [
						{ type: EDamageType.Physical, weight: 20 },
						{ type: EDamageType.Fire, weight: 80 }
					])
				]
			);
			const enemy = makeBattler(
				[
					{ id: EAttribute.Strength, amount: 10 },
					{ id: EAttribute.FireResistance, amount: 2.0 }
				],
				[]
			);

			const activations = battleStep(player, enemy, 40, noRng());

			expect(activations[0].damage).toBe(0);
			expect(enemy.currentHealth).toBe(100); // 100 → 80 (Physical) → 100 (Fire heal capped at 20 room)
		});

		it('reduces a single non-unit-weight portion to the single-type hit', () => {
			// raw × w ÷ w = raw exactly: a lone Fire portion at weight 2 of raw 20 deals 20 (no resistance).
			const player = makeBattler(baseStats, [makeMultiTypeSkill(20, 40, [{ type: EDamageType.Fire, weight: 2 }])]);
			const enemy = makeBattler([{ id: EAttribute.Strength, amount: 10 }], []);

			const activations = battleStep(player, enemy, 40, noRng());

			expect(activations[0].damage).toBe(20);
			expect(enemy.currentHealth).toBe(100 - 20);
		});
	});
});
