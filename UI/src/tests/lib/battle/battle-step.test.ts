import { describe, it, expect, beforeEach, vi } from 'vitest';
import { EAttribute, EModifierType, ESkillEffectTarget } from '$lib/api';
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
import { battlerFactory, makeSkill, makeEffect } from './battle-sim-test-utils';

const makeBattler = battlerFactory(mockSkills);

const newLog = (): BattleStepLog => ({
	appliedEffects: [],
	enemyDotDamage: 0,
	playerDotDamage: 0,
	enemyHotHeal: 0,
	playerHotHeal: 0
});
// A combatant with no Strength/Endurance has MaxHealth=50, Def=2 (the derived-stat floor).
const baseStats = [{ id: EAttribute.Endurance, amount: 0 }];

describe('battleStep', () => {
	beforeEach(() => {
		mockSkills.length = 0;
	});

	it('fires the player’s ready skills and damages the enemy', () => {
		const player = makeBattler(baseStats, [makeSkill(30, 1000)]);
		const enemy = makeBattler(baseStats, []);
		const enemyHealth = enemy.currentHealth;

		const activations = battleStep(player, enemy, 1000);

		expect(activations).toHaveLength(1);
		expect(activations[0].byPlayer).toBe(true);
		expect(activations[0].damage).toBe(28); // 30 raw - 2 def
		expect(enemy.currentHealth).toBe(enemyHealth - 28);
	});

	it('lets the enemy fire back when it survives the player’s hit', () => {
		const player = makeBattler(baseStats, [makeSkill(30, 1000)]);
		const enemy = makeBattler(baseStats, [makeSkill(20, 1000)]);
		const playerHealth = player.currentHealth;

		const activations = battleStep(player, enemy, 1000);

		expect(activations.map((a) => a.byPlayer)).toEqual([true, false]);
		expect(activations[1].damage).toBe(18); // 20 raw - 2 def
		expect(player.currentHealth).toBe(playerHealth - 18);
	});

	it('clamps damage at zero when defense exceeds the raw hit', () => {
		const player = makeBattler(baseStats, [makeSkill(5, 1000)]);
		const enemy = makeBattler([{ id: EAttribute.Endurance, amount: 100 }], []);
		const enemyHealth = enemy.currentHealth;

		const activations = battleStep(player, enemy, 1000);

		expect(activations[0].damage).toBe(0);
		expect(enemy.currentHealth).toBe(enemyHealth);
	});

	it('does not let a dead enemy counterattack in the same tick', () => {
		const player = makeBattler(baseStats, [makeSkill(100, 1000)]); // one-shots the 50-HP enemy
		const enemy = makeBattler(baseStats, [makeSkill(20, 1000)]);
		const playerHealth = player.currentHealth;

		const activations = battleStep(player, enemy, 1000);

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

		const activations = battleStep(player, enemy, 40);

		expect(activations).toHaveLength(0);
	});

	describe('log sink', () => {
		it('records a newly-applied effect and the side it landed on', () => {
			// The player's skill debuffs the enemy's Defense (an Opponent-targeted effect).
			const player = makeBattler(baseStats, [
				makeSkill(
					0,
					1000,
					[],
					[makeEffect(1, ESkillEffectTarget.Opponent, EAttribute.Defense, EModifierType.Additive, -5, 1000)]
				)
			]);
			const enemy = makeBattler(baseStats, []);
			const log = newLog();

			battleStep(player, enemy, 1000, log);

			expect(log.appliedEffects).toHaveLength(1);
			expect(log.appliedEffects[0].effect.id).toBe(1);
			expect(log.appliedEffects[0].onPlayer).toBe(false); // it landed on the enemy
		});

		it('does not record a refreshed effect as a new application', () => {
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

			battleStep(player, enemy, 40, log); // fires → applies → recorded
			expect(log.appliedEffects).toHaveLength(1);
			expect(log.appliedEffects[0].onPlayer).toBe(true);

			battleStep(player, enemy, 40, log); // fires again while still active → refresh, not recorded
			expect(log.appliedEffects).toHaveLength(0);
		});

		it('reports per-tick DoT on one side and HoT on the other', () => {
			// Enemy bleeds 12/s; player regenerates 8/s — both authored straight onto the battler.
			const player = makeBattler([{ id: EAttribute.HealthRegenPerSecond, amount: 8 }, ...baseStats], []);
			const enemy = makeBattler([{ id: EAttribute.DamageTakenPerSecond, amount: 12 }, ...baseStats], []);
			player.currentHealth -= 20; // leave room so the regen is not capped away
			const log = newLog();

			battleStep(player, enemy, 1000, log);

			expect(log.enemyDotDamage).toBe(12); // 12/s × 1000ms
			expect(log.playerHotHeal).toBe(8); // 8/s × 1000ms, room available
			expect(log.playerDotDamage).toBe(0);
			expect(log.enemyHotHeal).toBe(0);
		});

		it('resets the sink at the start of each tick', () => {
			const player = makeBattler(baseStats, []);
			const bleedingEnemy = makeBattler([{ id: EAttribute.DamageTakenPerSecond, amount: 12 }, ...baseStats], []);
			const cleanEnemy = makeBattler(baseStats, []);
			const log = newLog();

			battleStep(player, bleedingEnemy, 1000, log);
			expect(log.enemyDotDamage).toBe(12);

			// Re-running with an enemy that has no DoT clears the previous value rather than accumulating.
			battleStep(player, cleanEnemy, 1000, log);
			expect(log.enemyDotDamage).toBe(0);
		});
	});
});
