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

import { BattleSimulator } from '$lib/battle';
import { battlerFactory, makeSkill } from './battle-sim-test-utils';

const msPerTick = 40;
const makeBattler = battlerFactory(mockSkills);
// A combatant with no Strength/Endurance has MaxHealth=50, Def=2 (the derived-stat floor).
const baseStats = [{ id: EAttribute.Endurance, amount: 0 }];

// Outcomes the cross-implementation parity matrix doesn't cover on its own — most
// notably the player-death branch, which no parity scenario exercises.
describe('BattleSimulator', () => {
	beforeEach(() => {
		mockSkills.length = 0;
	});

	it('reports victory on the tick the enemy dies', () => {
		const player = makeBattler(baseStats, [makeSkill(100, 40)]); // one-shots on the first tick
		const enemy = makeBattler(baseStats, []);

		const result = new BattleSimulator(player, enemy).simulate();

		expect(result).toEqual({ victory: true, playerDied: false, totalMs: 40 });
	});

	it('reports the player dying when the enemy lands the killing blow first', () => {
		const player = makeBattler(baseStats, []); // never damages the enemy
		const enemy = makeBattler(baseStats, [makeSkill(100, 40)]);

		const result = new BattleSimulator(player, enemy).simulate();

		expect(result).toEqual({ victory: false, playerDied: true, totalMs: 40 });
	});

	it('runs to the default timeout when neither side can deal damage', () => {
		const player = makeBattler([{ id: EAttribute.Endurance, amount: 50 }], [makeSkill(5, 1000)]);
		const enemy = makeBattler([{ id: EAttribute.Endurance, amount: 50 }], [makeSkill(5, 1000)]);

		const result = new BattleSimulator(player, enemy).simulate();

		expect(result).toEqual({ victory: false, playerDied: false, totalMs: msPerTick * 10000 });
	});

	it('stops at maxMs (not maxMs + one tick) when capped before a kill', () => {
		const player = makeBattler(baseStats, [makeSkill(100, 1200)]); // can't fire before the cap
		const enemy = makeBattler(baseStats, [makeSkill(100, 1200)]);

		const result = new BattleSimulator(player, enemy).simulate(200);

		expect(result).toEqual({ victory: false, playerDied: false, totalMs: 200 });
	});
});
