import { describe, it, expect, beforeEach, vi } from 'vitest';
import { EAttribute } from '$lib/api';
import type { ISkill } from '$lib/api';

const mockSkills: ISkill[] = [];
vi.mock('$stores', () => ({
	staticData: {
		get skills() {
			return mockSkills;
		}
	}
}));

import { battleStep } from '$lib/battle';
import { battlerFactory, makeSkill } from './battle-sim-test-utils';

const makeBattler = battlerFactory(mockSkills);
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
});
