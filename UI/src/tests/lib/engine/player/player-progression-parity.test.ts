import { describe, it, expect, vi } from 'vitest';
import { PlayerManager } from '$lib/engine/player/player-manager';

// PlayerManager.grantExp logs via logMessage; mock it so the progression math is exercised in isolation
// (mirrors player-manager.test.ts).
vi.mock('$lib/engine/log', () => ({
	logMessage: vi.fn()
}));

// ── Shared progression parity matrix ────────────────────────────────────────────
// Every scenario here MUST mirror — with identical inputs and identical expected
// level/exp/statPointsGained — a row in the backend suite
// Game.Core.Tests/Players/PlayerProgressionParityTests.cs (the `Scenarios` table).
// The exp curve (EXP_PER_LEVEL) and per-level stat-point award (STAT_POINTS_PER_LEVEL) are shared
// across the boundary (#304), so a change to either must keep both suites in step.
//
// The backend additionally clamps a single grant to ServerGameConstants.MaxExpPerGrant as a server-only
// anti-cheat backstop; the frontend deliberately does not (the server is authoritative). Every shared
// scenario therefore grants well below that ceiling so the two sides agree.

interface ProgressionScenario {
	name: string;
	startLevel: number;
	startExp: number;
	startStatPoints: number;
	grant: number;
	expected: { level: number; exp: number; statPointsGained: number };
}

// Stat points accrue at STAT_POINTS_PER_LEVEL (the reduced free pool, currently 2) per level gained;
// each scenario's starting stat points are (startLevel - 1) × that rate.
const scenarios: ProgressionScenario[] = [
	// Below the level threshold: exp accrues, no level-up, no stat points.
	{
		name: 'belowThreshold',
		startLevel: 1,
		startExp: 0,
		startStatPoints: 0,
		grant: 50,
		expected: { level: 1, exp: 50, statPointsGained: 0 }
	},
	// Exactly at the threshold (>= 100) levels once with no carryover.
	{
		name: 'exactThreshold',
		startLevel: 1,
		startExp: 0,
		startStatPoints: 0,
		grant: 100,
		expected: { level: 2, exp: 0, statPointsGained: 2 }
	},
	// One level with carryover exp.
	{
		name: 'thresholdWithCarryover',
		startLevel: 1,
		startExp: 0,
		startStatPoints: 0,
		grant: 101,
		expected: { level: 2, exp: 1, statPointsGained: 2 }
	},
	// Spans two levels in one grant (100 + 200 = 300 to reach level 3).
	{
		name: 'twoLevels',
		startLevel: 1,
		startExp: 0,
		startStatPoints: 0,
		grant: 301,
		expected: { level: 3, exp: 1, statPointsGained: 4 }
	},
	// Starts mid-level with existing exp and stat points: one more level, points accumulate.
	{
		name: 'partialStartExp',
		startLevel: 2,
		startExp: 50,
		startStatPoints: 2,
		grant: 199,
		expected: { level: 3, exp: 49, statPointsGained: 4 }
	},
	// Multi-level from a higher level (thresholds 300 and 400 consumed; 500 not reached).
	{
		name: 'multiLevelFromHigherLevel',
		startLevel: 3,
		startExp: 0,
		startStatPoints: 4,
		grant: 1000,
		expected: { level: 5, exp: 300, statPointsGained: 8 }
	},
	// A large but sub-clamp grant levels many times: thresholds 100..900 consumed, 500 left.
	{
		name: 'largeGrantManyLevels',
		startLevel: 1,
		startExp: 0,
		startStatPoints: 0,
		grant: 5000,
		expected: { level: 10, exp: 500, statPointsGained: 18 }
	},
	// A grant just shy of a high-level threshold (10 * 100) does not level up.
	{
		name: 'noLevelAtHighLevel',
		startLevel: 10,
		startExp: 0,
		startStatPoints: 18,
		grant: 999,
		expected: { level: 10, exp: 999, statPointsGained: 18 }
	}
];

describe('Player progression parity with backend', () => {
	for (const scenario of scenarios) {
		it(`matches the backend for the ${scenario.name} scenario`, () => {
			const manager = new PlayerManager();
			manager.level = scenario.startLevel;
			manager.exp = scenario.startExp;
			manager.statPointsGained = scenario.startStatPoints;

			manager.grantExp(scenario.grant);

			expect(manager.level).toBe(scenario.expected.level);
			expect(manager.exp).toBe(scenario.expected.exp);
			expect(manager.statPointsGained).toBe(scenario.expected.statPointsGained);
		});
	}
});
