import { describe, it, expect, beforeEach, vi } from 'vitest';
import { EAttribute } from '$lib/api';
import type { ISkill } from '$lib/api';

// Drive the REAL production simulation. The mocked `staticData.skills` is the
// registry `makeBattler` registers each scenario's skills into, so the Battlers
// resolve their skills exactly as the live game does.
const mockSkills: ISkill[] = [];
vi.mock('$stores', () => ({
	staticData: {
		get skills() {
			return mockSkills;
		}
	}
}));

import { BattleSimulator, Battler } from '$lib/battle';
import type { BattleResult } from '$lib/battle';
import { battlerFactory, makeSkill } from './battle-sim-test-utils';

/**
 * The parity matrix below asserts the full outcome (victory / playerDied /
 * totalMs) of the production `BattleSimulator` — the same code the live
 * BattleEngine runs per tick — against the backend's. Building real Battlers and
 * running the real simulator (rather than a hand-rolled copy) means a change to
 * the per-tick arithmetic that diverges from the backend can no longer pass here.
 */
const msPerTick = 40;
const makeBattler = battlerFactory(mockSkills);

// ── Shared parity matrix ───────────────────────────────────────────────────────
// Every scenario here MUST mirror — with identical inputs and identical expected
// totalMs/outcome — a row in the backend suite
// Game.Core.Tests/Battle/BattleSimulatorParityTests.cs (the `Scenarios` table).
// The two simulators run on both the client and the server, so a divergence here
// would let the anti-cheat replay disagree with the live battle.

interface ParityScenario {
	name: string;
	player: () => Battler;
	enemy: () => Battler;
	maxMs?: number;
	expected: BattleResult;
}

const scenarios: ParityScenario[] = [
	// Single skill, CooldownRecovery > 0 — exercises the cdMultiplier path.
	//   Player: MaxHealth=900, Def=42, CDR=9 → cdMult=1.09; damage 85, after def 68.
	//   Enemy:  MaxHealth=400, Def=17; damage 5-42 clamped to 0.
	//   6 hits at ticks 28,56,84,112,140,168 → 6720ms.
	{
		name: 'cooldownRecovery',
		player: () =>
			makeBattler(
				[
					{ id: EAttribute.Strength, amount: 50 },
					{ id: EAttribute.Endurance, amount: 30 },
					{ id: EAttribute.Agility, amount: 20 },
					{ id: EAttribute.Dexterity, amount: 10 }
				],
				[makeSkill(10, 1200, [{ attributeId: EAttribute.Strength, multiplier: 1.5 }])]
			),
		enemy: () =>
			makeBattler(
				[
					{ id: EAttribute.Strength, amount: 10 },
					{ id: EAttribute.Endurance, amount: 15 }
				],
				[makeSkill(5, 2000)]
			),
		expected: { victory: true, playerDied: false, totalMs: 6720 }
	},

	// Two player skills on different cooldowns vs an enemy that deals real damage.
	//   Player: MaxHealth=600, Def=22; skillA 50 raw→33 every 20 ticks, skillB 25 raw→8 every 30 ticks.
	//   Enemy:  MaxHealth=450, Def=17; attack 30 raw→8 every 25 ticks.
	//   Cumulative player damage first reaches 450 at tick 240 → 9600ms.
	{
		name: 'multiSkill',
		player: () =>
			makeBattler(
				[
					{ id: EAttribute.Strength, amount: 30 },
					{ id: EAttribute.Endurance, amount: 20 }
				],
				[makeSkill(20, 800, [{ attributeId: EAttribute.Strength, multiplier: 1.0 }]), makeSkill(25, 1200)]
			),
		enemy: () =>
			makeBattler(
				[
					{ id: EAttribute.Strength, amount: 20 },
					{ id: EAttribute.Endurance, amount: 15 }
				],
				[makeSkill(30, 1000)]
			),
		expected: { victory: true, playerDied: false, totalMs: 9600 }
	},

	// Both sides have so much Defense that every hit clamps to 0 — runs to timeout.
	//   Player/Enemy: Def=52 each; attacks 5 raw → 5-52 clamped to 0.
	{
		name: 'highDefenseFloor',
		player: () => makeBattler([{ id: EAttribute.Endurance, amount: 50 }], [makeSkill(5, 1000)]),
		enemy: () => makeBattler([{ id: EAttribute.Endurance, amount: 50 }], [makeSkill(5, 1000)]),
		expected: { victory: false, playerDied: false, totalMs: msPerTick * 10000 }
	},

	// Neither side has skills, so no damage is dealt — runs to the default timeout.
	{
		name: 'noSkills',
		player: () =>
			makeBattler(
				[
					{ id: EAttribute.Strength, amount: 10 },
					{ id: EAttribute.Endurance, amount: 10 }
				],
				[]
			),
		enemy: () =>
			makeBattler(
				[
					{ id: EAttribute.Strength, amount: 10 },
					{ id: EAttribute.Endurance, amount: 10 }
				],
				[]
			),
		expected: { victory: false, playerDied: false, totalMs: msPerTick * 10000 }
	},

	// A dedicated-boss encounter: a higher-level boss bringing its FULL authored loadout (3 skills)
	// against a player who out-tanks it. Mirrors the backend `bossFullLoadout` scenario.
	//   Player: Str=60, End=40 → MaxHealth=1150, Def=42; skill 130 raw → 68 after boss def, every 25 ticks.
	//   Boss:   Str=20, End=60 → MaxHealth=1350, Def=62; only the 50-dmg skill pierces (8 dmg), rest clamp to 0.
	//   20 player hits reach 1360 ≥ 1350 at tick 500 → 20000ms; the boss never threatens the player.
	{
		name: 'bossFullLoadout',
		player: () =>
			makeBattler(
				[
					{ id: EAttribute.Strength, amount: 60 },
					{ id: EAttribute.Endurance, amount: 40 }
				],
				[makeSkill(10, 1000, [{ attributeId: EAttribute.Strength, multiplier: 2.0 }])]
			),
		enemy: () =>
			makeBattler(
				[
					{ id: EAttribute.Strength, amount: 20 },
					{ id: EAttribute.Endurance, amount: 60 }
				],
				[makeSkill(50, 1000), makeSkill(20, 1000), makeSkill(30, 1000)]
			),
		expected: { victory: true, playerDied: false, totalMs: 20000 }
	},

	// The cooldownRecovery matchup capped before either skill fires: stops at maxMs.
	{
		name: 'maxMsCap',
		player: () =>
			makeBattler(
				[
					{ id: EAttribute.Strength, amount: 50 },
					{ id: EAttribute.Endurance, amount: 30 },
					{ id: EAttribute.Agility, amount: 20 },
					{ id: EAttribute.Dexterity, amount: 10 }
				],
				[makeSkill(10, 1200, [{ attributeId: EAttribute.Strength, multiplier: 1.5 }])]
			),
		enemy: () =>
			makeBattler(
				[
					{ id: EAttribute.Strength, amount: 10 },
					{ id: EAttribute.Endurance, amount: 15 }
				],
				[makeSkill(5, 2000)]
			),
		maxMs: 200,
		expected: { victory: false, playerDied: false, totalMs: 200 }
	}
];

describe('Battle simulation parity with backend', () => {
	beforeEach(() => {
		mockSkills.length = 0;
	});

	for (const scenario of scenarios) {
		it(`matches the backend for the ${scenario.name} scenario`, () => {
			const sim = new BattleSimulator(scenario.player(), scenario.enemy());
			const result = sim.simulate(scenario.maxMs);

			expect(result).toEqual(scenario.expected);
			expect(result.totalMs % msPerTick).toBe(0);
		});
	}

	it('resolves the cooldownRecovery derived stats to the expected values', () => {
		const player = scenarios[0].player();
		const enemy = scenarios[0].enemy();

		expect(player.attributes.getValue(EAttribute.MaxHealth)).toBe(900);
		expect(player.attributes.getValue(EAttribute.Defense)).toBe(42);
		expect(player.attributes.getValue(EAttribute.CooldownRecovery)).toBe(9);
		expect(player.cdMultiplier).toBeCloseTo(1.09, 10);

		expect(enemy.attributes.getValue(EAttribute.MaxHealth)).toBe(400);
		expect(enemy.attributes.getValue(EAttribute.Defense)).toBe(17);
	});

	it('ends sooner if the player double-counts derived stats (the historical bug)', () => {
		// The frontend used to pass the API's already-final attribute values to
		// BattleAttributes with calcDerivedStats=true, adding derived stats AGAIN.
		// Mirrors BattleSimulatorParityTests.Parity_DoubleDerivedStats_ProducesShorterBattle.
		const playerFinalAttrs = [
			{ id: EAttribute.Strength, amount: 50 },
			{ id: EAttribute.Endurance, amount: 30 },
			{ id: EAttribute.Agility, amount: 20 },
			{ id: EAttribute.Dexterity, amount: 10 },
			// The FINAL values including derived stats, re-fed into the derived pipeline:
			{ id: EAttribute.MaxHealth, amount: 900 }, // 50 + 20*30 + 5*50
			{ id: EAttribute.Defense, amount: 42 }, // 2 + 30 + 0.5*20
			{ id: EAttribute.CooldownRecovery, amount: 9 } // 0.4*20 + 0.1*10
		];
		const player = makeBattler(playerFinalAttrs, [
			makeSkill(10, 1200, [{ attributeId: EAttribute.Strength, multiplier: 1.5 }])
		]);
		const enemy = scenarios[0].enemy();

		// Derived stats are now DOUBLED.
		expect(player.attributes.getValue(EAttribute.MaxHealth)).toBe(1800);
		expect(player.attributes.getValue(EAttribute.Defense)).toBe(84);
		expect(player.attributes.getValue(EAttribute.CooldownRecovery)).toBe(18);
		expect(player.cdMultiplier).toBeCloseTo(1.18, 10);

		const result = new BattleSimulator(player, enemy).simulate();
		expect(result.totalMs).toBe(6240);
		expect(result.totalMs).toBeLessThan(6720);
	});
});
