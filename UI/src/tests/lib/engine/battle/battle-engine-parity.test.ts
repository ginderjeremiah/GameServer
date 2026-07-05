import { describe, it, expect, beforeEach, vi } from 'vitest';
import { EAttribute, type IBattlerAttribute, type IEnemy, type ISkill } from '$lib/api';
import { DEFAULT_MAX_BATTLE_MS } from '$lib/api/types/game-constants';

// Both consumers of the shared `battleStep` — the live, render-driven `BattleEngine.logicalUpdate` and the
// headless `BattleSimulator` — must produce the same outcome for the same inputs and seed, since the backend
// anti-cheat replay drives the simulator path. The existing battle-simulation-parity suite only exercises the
// simulator; this smoke test runs a few representative scenarios through the LIVE engine and asserts it agrees
// with the simulator, so any per-tick logic added to the live loop OUTSIDE `battleStep` (which would silently
// escape the parity matrix) fails here instead (#1365).

// The shared skill registry the mocked `staticData.skills` returns; both the engine's player/enemy battlers and
// the comparison simulator battlers resolve their skills out of it by id, exactly as the live game does.
const mockSkills: ISkill[] = [];
const mockEnemies: IEnemy[] = [];
const mockAttributes: { id: number; name: string }[] = [{ id: 0, name: 'Strength' }];

// Captured logical-update hook — driving the engine means invoking this with fixed `tickSize` deltas.
type DeltaCallback = (delta: number) => void;
type EnemyLoadedCallback = (enemy: import('$lib/api').IEnemyInstance) => void;
let logicalUpdateCallbacks: DeltaCallback[] = [];
let enemyLoadedCallbacks: EnemyLoadedCallback[] = [];

// The player's derivation inputs (#811): attributes / loadout / level, plus the class + proficiency modifier
// seams. Held to no-ops here so the engine's player battler matches a simulator battler built from the raw
// allocations alone — a flat additive-0 signature passive leaves every attribute untouched (raw enum values:
// type 1 = Additive, source 9 = Class), and the locked-base / proficiency modifier lists are empty.
const mockPlayerManager = {
	name: 'TestPlayer',
	level: 1,
	selectedSkills: [] as number[],
	attributes: [] as IBattlerAttribute[],
	battleLockedBaseModifiers: [] as unknown[],
	battleSignaturePassiveModifier: () => ({ attribute: 0, amount: 0, type: 1, source: 9 })
};
// The weapon-match gate's equipped weapon type is left undefined so the player is ungated, matching the
// comparison simulator battlers (which field their full loadout); the scenarios use weapon-agnostic skills.
const mockInventoryManager = {
	equipmentStats: [] as IBattlerAttribute[],
	grantedSkillIds: [] as number[],
	equippedWeaponType: undefined as import('$lib/api').EDamageType | undefined
};
const mockPlayerProficiencies = { battleModifiers: [] as unknown[] };

vi.mock('svelte', async (importOriginal) => ({
	...((await importOriginal()) as Record<string, unknown>),
	onDestroy: vi.fn()
}));

vi.mock('$stores', () => ({
	staticData: {
		get skills() {
			return mockSkills;
		},
		get enemies() {
			return mockEnemies;
		},
		get attributes() {
			return mockAttributes;
		}
	},
	playerProficiencies: {
		get battleModifiers() {
			return mockPlayerProficiencies.battleModifiers;
		}
	}
}));

vi.mock('$lib/engine/log', () => ({ logMessage: vi.fn() }));

vi.mock('$lib/engine/engine', () => ({
	get inventoryManager() {
		return mockInventoryManager;
	}
}));

vi.mock('$lib/engine/player/player-manager', () => ({
	get playerManager() {
		return mockPlayerManager;
	}
}));

vi.mock('$lib/engine/logical-engine', () => ({
	// Mirrors the real exported constant (MS_PER_TICK); BattleSimulator reads it for its tick size.
	tickSize: 40,
	LogicalEngine: vi.fn(class {}),
	onLogicalUpdate: vi.fn((cb: DeltaCallback) => {
		logicalUpdateCallbacks.push(cb);
		return () => {
			logicalUpdateCallbacks = logicalUpdateCallbacks.filter((c) => c !== cb);
		};
	})
}));

vi.mock('$lib/engine/render-engine', () => ({
	RenderEngine: vi.fn(class {}),
	onRenderUpdate: vi.fn(() => () => {})
}));

vi.mock('$lib/engine/battle/enemy-manager', () => ({
	onNewEnemyLoaded: vi.fn((cb: EnemyLoadedCallback) => {
		enemyLoadedCallbacks.push(cb);
		return () => {
			enemyLoadedCallbacks = enemyLoadedCallbacks.filter((c) => c !== cb);
		};
	})
}));

import { BattleEngine, BattleStage } from '$lib/engine/battle/battle-engine';
import { BattleSimulator } from '$lib/battle';
import type { BattleResult } from '$lib/battle';
import { tickSize } from '$lib/engine/logical-engine';
import { makeSkill, grantedBattlerFactory, type SkillSpec } from '../../battle/battle-sim-test-utils';

/** The shared battle-RNG seed both consumers construct their Mulberry32 from — mirrors the simulator parity
 *  suite's PARITY_SEED, so the fractional crit/dodge draws line up with that matrix. */
const PARITY_SEED = 0x9e3779b9;

const granted = grantedBattlerFactory(mockSkills);

interface EngineParityScenario {
	name: string;
	playerAttrs: { id: EAttribute; amount: number }[];
	playerSkills: SkillSpec[];
	enemyAttrs: { id: EAttribute; amount: number }[];
	enemySkills: SkillSpec[];
	/** The documented outcome from the simulator parity matrix — a cross-check that the scenario was
	 *  transcribed faithfully, on top of the engine-vs-simulator agreement that is the real assertion. */
	expected: BattleResult;
}

// A representative slice of the simulator parity matrix covering all three terminal outcomes — a victory that
// exercises the seeded fractional crit AND an enemy firing back, a player defeat driven by DoT/HoT, and a
// timeout draw — each with identical inputs/expected to its row in battle-simulation-parity.test.ts.
const scenarios: EngineParityScenario[] = [
	// drawOrderDodgeOnlyAlignsCrit: real skill CriticalChance 0.5 (so the outcome depends on the exact
	// seeded stream) against an enemy that fires every tick — pins both the seed lockstep and the
	// enemy-attack path.
	{
		name: 'drawOrderDodgeOnlyAlignsCrit',
		playerAttrs: [
			{ id: EAttribute.Strength, amount: 10 },
			{ id: EAttribute.CriticalDamage, amount: 0.5 }
		],
		playerSkills: [makeSkill(12, 400, [], [], undefined, 0.5)],
		enemyAttrs: [{ id: EAttribute.Strength, amount: 6 }],
		enemySkills: [makeSkill(5, 400)],
		expected: { victory: true, playerDied: false, totalMs: 1600 }
	},
	// dotRegenNetNegativeKillsAfterHeal: the player's net-negative DoT/HoT grinds it down — a defeat with no
	// direct hits, exercising the live loop's end-of-tick DoT/HoT + player-death branch.
	{
		name: 'dotRegenNetNegativeKillsAfterHeal',
		playerAttrs: [
			{ id: EAttribute.Endurance, amount: 0 },
			{ id: EAttribute.PoisonDamagePerSecond, amount: 250 },
			{ id: EAttribute.HealthRegenPerSecond, amount: 150 }
		],
		playerSkills: [],
		enemyAttrs: [{ id: EAttribute.Endurance, amount: 0 }],
		enemySkills: [],
		expected: { victory: false, playerDied: true, totalMs: 520 }
	},
	// highToughnessTrickle: both sides out-tank the trickle, so neither lands the kill — the battle runs to the
	// 2-minute cap and ends as a draw, pinning the live loop's timeout branch against the simulator's.
	{
		name: 'highToughnessTrickle',
		playerAttrs: [{ id: EAttribute.Endurance, amount: 50 }],
		playerSkills: [makeSkill(5, 1000)],
		enemyAttrs: [{ id: EAttribute.Endurance, amount: 50 }],
		enemySkills: [makeSkill(5, 1000)],
		expected: { victory: false, playerDied: false, totalMs: DEFAULT_MAX_BATTLE_MS }
	}
];

const toBattlerAttributes = (attrs: { id: EAttribute; amount: number }[]): IBattlerAttribute[] =>
	attrs.map((a) => ({ attributeId: a.id, amount: a.amount }));

/** Maps a terminated engine stage to the simulator's {@link BattleResult} shape so the two are directly
 *  comparable. Throws if the battle never left the Active stage. */
function engineResult(stage: BattleStage, totalMs: number): BattleResult {
	switch (stage) {
		case BattleStage.Victorious:
			return { victory: true, playerDied: false, totalMs };
		case BattleStage.Defeated:
			return { victory: false, playerDied: true, totalMs };
		case BattleStage.Drawn:
			return { victory: false, playerDied: false, totalMs };
		default:
			throw new Error(`Battle did not terminate within the cap (stage ${stage})`);
	}
}

/** Drives the engine with fixed `tickSize` logical updates until the battle leaves the Active stage (the same
 *  loop the live render-driven loop runs), then reports the terminal outcome. The tick budget mirrors the
 *  simulator's `maxMs` cap so a non-terminating battle surfaces as a thrown error rather than an infinite loop. */
function runEngine(engine: BattleEngine): BattleResult {
	const maxTicks = DEFAULT_MAX_BATTLE_MS / tickSize + 1;
	let ticks = 0;
	while (engine.stage === BattleStage.Active && ticks < maxTicks) {
		logicalUpdateCallbacks[0](tickSize);
		ticks++;
	}
	return engineResult(engine.stage, engine.timeElapsed);
}

describe('BattleEngine parity with the headless BattleSimulator', () => {
	beforeEach(() => {
		logicalUpdateCallbacks = [];
		enemyLoadedCallbacks = [];
		mockSkills.length = 0;
		mockEnemies.length = 0;
	});

	for (const scenario of scenarios) {
		it(`matches the simulator for the ${scenario.name} scenario`, () => {
			const playerSkillIds = scenario.playerSkills.map((spec) => granted.register(spec));
			const enemySkillIds = scenario.enemySkills.map((spec) => granted.register(spec));

			// The reference outcome from the headless simulator, built from the same registered skills, raw
			// allocations, and seed the engine is about to run.
			const simResult = new BattleSimulator(
				granted.build(scenario.playerAttrs, playerSkillIds, []),
				granted.build(scenario.enemyAttrs, enemySkillIds, []),
				PARITY_SEED
			).simulate();
			// Cross-check the transcription against the documented matrix value before comparing consumers.
			expect(simResult).toEqual(scenario.expected);

			// Configure the live engine to derive the identical player/enemy battlers and run it to completion.
			mockPlayerManager.attributes = toBattlerAttributes(scenario.playerAttrs);
			mockPlayerManager.selectedSkills = playerSkillIds;
			mockEnemies[1] = {
				id: 1,
				name: 'Enemy',
				designerNotes: '',
				isBoss: false,
				attributeDistribution: [],
				skillPool: enemySkillIds,
				spawns: []
			};

			const engine = new BattleEngine();
			engine.start();
			enemyLoadedCallbacks[0]({
				id: 1,
				level: 1,
				seed: PARITY_SEED,
				selectedSkills: enemySkillIds,
				attributes: toBattlerAttributes(scenario.enemyAttrs),
				enemyRating: 100
			});

			expect(runEngine(engine)).toEqual(simResult);
		});
	}
});
