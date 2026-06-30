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
import { battlerFactory, makeSkill, makeMultiTypeSkill, makeEffect } from './battle-sim-test-utils';

const makeBattler = battlerFactory(mockSkills);
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
		// The curve asymptotes below 100%: Toughness 180 (Endurance 90) vs a level-1 attacker keeps
		// 20/(180 + 20) = 10% of a 5-damage hit, so 0.5 trickles through — the old flat-Defense clamp to 0 is gone.
		const player = makeBattler(baseStats, [makeSkill(5, 1000)]);
		const enemy = makeBattler([{ id: EAttribute.Endurance, amount: 90 }], []);
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
			player.takeDamage(45, EDamageType.Physical, 1); // no Toughness → 45 damage → currentHealth 5
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
			const player = makeBattler(
				[
					{ id: EAttribute.CriticalChance, amount: 1 },
					{ id: EAttribute.CriticalDamage, amount: 0.5 }
				],
				[makeSkill(20, 40)]
			);
			const enemy = makeBattler(baseStats, []);

			const activations = battleStep(player, enemy, 40, new Mulberry32(0));

			expect(activations[0].crit).toBe(true);
			expect(activations[0].damage).toBe(40); // 20×2, enemy Toughness 0
			expect(enemy.currentHealth).toBe(50 - 40);
		});

		it('deals raw damage when the player does not crit', () => {
			const player = makeBattler([{ id: EAttribute.CriticalChance, amount: 0 }], [makeSkill(20, 40)]);
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

		it('never crits on the enemy’s attack, even with a forced crit chance', () => {
			const player = makeBattler(baseStats, []);
			const enemy = makeBattler(
				[
					{ id: EAttribute.CriticalChance, amount: 1 },
					{ id: EAttribute.CriticalDamage, amount: 2 }
				],
				[makeSkill(20, 40)]
			);

			const activations = battleStep(player, enemy, 40, new Mulberry32(0));

			expect(activations[0].crit).toBe(false);
			expect(activations[0].damage).toBe(20); // 20 (no crit), NOT 40
		});

		it('draws once per player fire and once per enemy fire, in order', () => {
			// One crit draw for the player's hit, then one dodge draw for the enemy's — two draws total (Block's
			// second draw was retired, #1330).
			const seed = 12345;
			const rng = new Mulberry32(seed);
			const player = makeBattler(baseStats, [makeSkill(10, 40)]);
			const enemy = makeBattler([{ id: EAttribute.Endurance, amount: 100 }], [makeSkill(10, 40)]); // tanky, survives

			battleStep(player, enemy, 40, rng);

			const reference = new Mulberry32(seed);
			reference.next();
			reference.next();
			expect(rng.next()).toBe(reference.next());
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
			// CriticalDamage 1.5 + 0.5 = 2. One crit scales BOTH portions of [Physical 50, Fire 50] of raw 20 →
			// 10 each, ×2 → 20 each = 40 (a per-portion-only crit would give 30).
			const player = makeBattler(
				[
					{ id: EAttribute.CriticalChance, amount: 1 },
					{ id: EAttribute.CriticalDamage, amount: 0.5 }
				],
				[
					makeMultiTypeSkill(20, 40, [
						{ type: EDamageType.Physical, weight: 50 },
						{ type: EDamageType.Fire, weight: 50 }
					])
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
