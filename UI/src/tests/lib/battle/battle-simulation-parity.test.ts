import { describe, it, expect, beforeEach, vi } from 'vitest';
import { EAttribute, EItemModType, EModifierType, ESkillEffectTarget } from '$lib/api';
import type { ISkill, IItem, IItemMod } from '$lib/api';

// Drive the REAL production simulation. The mocked `staticData.skills` is the
// registry `makeBattler` registers each scenario's skills into, so the Battlers
// resolve their skills exactly as the live game does; `items` / `itemMods` back
// the registries `makeEquipment` builds equipped items + applied mods from.
const mockSkills: ISkill[] = [];
const mockItems: IItem[] = [];
const mockItemMods: IItemMod[] = [];
vi.mock('$stores', () => ({
	staticData: {
		get skills() {
			return mockSkills;
		},
		get items() {
			return mockItems;
		},
		get itemMods() {
			return mockItemMods;
		}
	}
}));

import { BattleSimulator, Battler } from '$lib/battle';
import type { BattleResult } from '$lib/battle';
import { DEFAULT_MAX_BATTLE_MS, MS_PER_TICK } from '$lib/api/types/game-constants';
import { battlerFactory, equipmentFactory, makeSkill, makeEffect } from './battle-sim-test-utils';

/** A duration long enough that an effect never expires within a battle (for "permanent" buffs). */
const PERMANENT = 1_000_000;

/**
 * The parity matrix below asserts the full outcome (victory / playerDied /
 * totalMs) of the production `BattleSimulator` — the same code the live
 * BattleEngine runs per tick — against the backend's. Building real Battlers and
 * running the real simulator (rather than a hand-rolled copy) means a change to
 * the per-tick arithmetic that diverges from the backend can no longer pass here.
 */
const makeBattler = battlerFactory(mockSkills);
const makeEquipment = equipmentFactory(mockItems, mockItemMods);

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
		expected: { victory: false, playerDied: false, totalMs: DEFAULT_MAX_BATTLE_MS }
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
		expected: { victory: false, playerDied: false, totalMs: DEFAULT_MAX_BATTLE_MS }
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
	},

	// The only scenario whose player is built through the equipped-item + applied-mod
	// attribute-composition path (the rest build the player from raw stat allocations).
	// An item and two mods (prefix + suffix) each contribute Strength, so dropping any
	// one source changes the kill count; the item's Agility and the prefix's Dexterity
	// additionally feed CooldownRecovery, so the whole merge is exercised end to end.
	//   Allocations: Str=20, End=20.  Item: +10 Str, +20 Agi.  Prefix: +8 Str, +20 Dex.  Suffix: +7 Str.
	//   Merged: Str=45, End=20, Agi=20, Dex=20 → MaxHealth=675, Def=32, CDR=10 → cdMult=1.10.
	//   Player skill: 10 + 45*1.5 = 77.5 raw, after enemy def 17 → 60.5, fires every 28 ticks
	//     (charge/tick = 40*1.10 = 44, 44*28=1232 ≥ 1200).
	//   Enemy: MaxHealth=400, Def=17; attack 5-32 clamps to 0, so the player never dies.
	//   7 hits reach 423.5 ≥ 400 at tick 196 → 7840ms (vs 21600ms for the same allocations
	//   with no equipment — proof the merged attributes change the outcome).
	{
		name: 'equippedItemWithMods',
		player: () =>
			makeBattler(
				[
					{ id: EAttribute.Strength, amount: 20 },
					{ id: EAttribute.Endurance, amount: 20 }
				],
				[makeSkill(10, 1200, [{ attributeId: EAttribute.Strength, multiplier: 1.5 }])],
				makeEquipment({
					attributes: [
						{ attributeId: EAttribute.Strength, amount: 10 },
						{ attributeId: EAttribute.Agility, amount: 20 }
					],
					mods: [
						{
							type: EItemModType.Prefix,
							attributes: [
								{ attributeId: EAttribute.Strength, amount: 8 },
								{ attributeId: EAttribute.Dexterity, amount: 20 }
							]
						},
						{
							type: EItemModType.Suffix,
							attributes: [{ attributeId: EAttribute.Strength, amount: 7 }]
						}
					]
				})
			),
		enemy: () =>
			makeBattler(
				[
					{ id: EAttribute.Strength, amount: 10 },
					{ id: EAttribute.Endurance, amount: 15 }
				],
				[makeSkill(5, 2000)]
			),
		expected: { victory: true, playerDied: false, totalMs: 7840 }
	},

	// A self Strength buff: the carrying hit uses the pre-effect attributes and only subsequent hits see
	// the boost (which also raises damage via the Strength→damage path); re-applying it each fire REFRESHES
	// rather than stacks. Mirrors the backend `selfStrengthBuffAffectsLaterHits` scenario.
	//   Player: Str=10, cdMult=1; skill = Str×1.0 raw, cooldown 400. Effect: Self +10 Strength, permanent.
	//   Enemy:  Str=10 → MaxHealth=100, Def=2, no skills.
	//   Hit 1 deals 10−2=8 (pre-buff); hits 2+ deal 20−2=18. Enemy HP 100 dies on hit 7 at 2800.
	{
		name: 'selfStrengthBuffAffectsLaterHits',
		player: () =>
			makeBattler(
				[{ id: EAttribute.Strength, amount: 10 }],
				[
					makeSkill(
						0,
						400,
						[{ attributeId: EAttribute.Strength, multiplier: 1.0 }],
						[makeEffect(100, ESkillEffectTarget.Self, EAttribute.Strength, EModifierType.Additive, 10, PERMANENT)]
					)
				]
			),
		enemy: () => makeBattler([{ id: EAttribute.Strength, amount: 10 }], []),
		expected: { victory: true, playerDied: false, totalMs: 2800 }
	},

	// A self CooldownRecovery buff is read live each tick, so it shortens the fire interval after the first
	// hit applies it. Mirrors the backend `cdrBuffShortensFireInterval` scenario.
	//   Player: Str=20, base CDR=0 (cdMult=1); skill = Str×1.0 raw, cooldown 400.
	//     Effect: Self +100 CooldownRecovery → cdMult=2 once applied, permanent.
	//   Enemy:  Str=10 → MaxHealth=100, Def=2, no skills. Each hit deals 18.
	//   Hit 1 at 400 applies the buff; thereafter the skill fires every 200ms → hit 6 at 1400.
	{
		name: 'cdrBuffShortensFireInterval',
		player: () =>
			makeBattler(
				[{ id: EAttribute.Strength, amount: 20 }],
				[
					makeSkill(
						0,
						400,
						[{ attributeId: EAttribute.Strength, multiplier: 1.0 }],
						[
							makeEffect(
								101,
								ESkillEffectTarget.Self,
								EAttribute.CooldownRecovery,
								EModifierType.Additive,
								100,
								PERMANENT
							)
						]
					)
				]
			),
		enemy: () => makeBattler([{ id: EAttribute.Strength, amount: 10 }], []),
		expected: { victory: true, playerDied: false, totalMs: 1400 }
	},

	// Same-tick ordering: slot 0's self buff (applied after its own pre-buff hit) influences slot 1 firing
	// after it on the same tick. Mirrors the backend `sameTickEarlierSlotBuffsLaterSlot` scenario.
	//   Player: Str=10; slot0 = Str×1.0 (Self +10 Str, permanent), slot1 = Str×1.0 (no effect), both cooldown 400.
	//   Enemy:  Str=16 → MaxHealth=130, Def=2, no skills.
	//   Tick 400 deals 8 (slot0) + 18 (slot1, buffed) = 26; later ticks deal 36. Enemy HP 130 dies at 1600.
	{
		name: 'sameTickEarlierSlotBuffsLaterSlot',
		player: () =>
			makeBattler(
				[{ id: EAttribute.Strength, amount: 10 }],
				[
					makeSkill(
						0,
						400,
						[{ attributeId: EAttribute.Strength, multiplier: 1.0 }],
						[makeEffect(102, ESkillEffectTarget.Self, EAttribute.Strength, EModifierType.Additive, 10, PERMANENT)]
					),
					makeSkill(0, 400, [{ attributeId: EAttribute.Strength, multiplier: 1.0 }])
				]
			),
		enemy: () => makeBattler([{ id: EAttribute.Strength, amount: 16 }], []),
		expected: { victory: true, playerDied: false, totalMs: 1600 }
	},

	// MaxHealth clamp: an Opponent MaxHealth ×0.5 debuff halves the enemy's maximum and clamps its current
	// health down to it. Mirrors the backend `opponentMaxHealthDebuffClamps` scenario.
	//   Player: skill baseDamage 12, no multiplier → 12−2 = 10 per hit, cooldown 400.
	//     Effect: Opponent ×0.5 MaxHealth (multiplicative), permanent.
	//   Enemy:  Str=10 → MaxHealth=100, Def=2, no skills.
	//   Tick 400 deals 10 (100→90) then halves MaxHealth to 50 → clamps to 50, then 50→0 → dies at 2400.
	{
		name: 'opponentMaxHealthDebuffClamps',
		player: () =>
			makeBattler(
				[],
				[
					makeSkill(
						12,
						400,
						[],
						[
							makeEffect(
								103,
								ESkillEffectTarget.Opponent,
								EAttribute.MaxHealth,
								EModifierType.Multiplicative,
								0.5,
								PERMANENT
							)
						]
					)
				]
			),
		enemy: () => makeBattler([{ id: EAttribute.Strength, amount: 10 }], []),
		expected: { victory: true, playerDied: false, totalMs: 2400 }
	},

	// Same-tick CDR ordering: slot 0's self CooldownRecovery buff speeds up slot 1's accrual on the same
	// tick, because each slot reads the cooldown multiplier live in loadout order. Mirrors the backend
	// `cdrBuffSpeedsLaterSlotSameTick` scenario.
	//   Player: base CDR=0. slot0 = pure buffer (0 dmg, cooldown 40, Self +100 CDR permanent → cdMult=2),
	//     slot1 = baseDamage 27, cooldown 400. Enemy Str=5 → MaxHealth=75, Def=2, no skills (25/hit).
	//   The boosted accrual makes slot1 fire at 200,400,600 → enemy dies on the 3rd hit at 600.
	{
		name: 'cdrBuffSpeedsLaterSlotSameTick',
		player: () =>
			makeBattler(
				[],
				[
					makeSkill(
						0,
						40,
						[],
						[
							makeEffect(
								104,
								ESkillEffectTarget.Self,
								EAttribute.CooldownRecovery,
								EModifierType.Additive,
								100,
								PERMANENT
							)
						]
					),
					makeSkill(27, 400)
				]
			),
		enemy: () => makeBattler([{ id: EAttribute.Strength, amount: 5 }], []),
		expected: { victory: true, playerDied: false, totalMs: 600 }
	}
];

describe('Battle simulation parity with backend', () => {
	beforeEach(() => {
		mockSkills.length = 0;
		mockItems.length = 0;
		mockItemMods.length = 0;
	});

	for (const scenario of scenarios) {
		it(`matches the backend for the ${scenario.name} scenario`, () => {
			const sim = new BattleSimulator(scenario.player(), scenario.enemy());
			const result = sim.simulate(scenario.maxMs);

			expect(result).toEqual(scenario.expected);
			expect(result.totalMs % MS_PER_TICK).toBe(0);
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

	it('composes the equippedItemWithMods stat + item + mod attributes to the expected values', () => {
		// Pins the merged values down so a divergence in item/mod attribute merging
		// (rather than just the battle outcome) is reported directly. Mirrors
		// BattleSimulatorParityTests.Parity_EquippedItemWithMods_ComposesMergedAttributes.
		const scenario = scenarios.find((s) => s.name === 'equippedItemWithMods');
		if (!scenario) {
			throw new Error('equippedItemWithMods scenario is missing from the matrix');
		}
		const player = scenario.player();

		// Strength 20 (alloc) + 10 (item) + 8 (prefix) + 7 (suffix) = 45.
		expect(player.attributes.getValue(EAttribute.Strength)).toBe(45);
		expect(player.attributes.getValue(EAttribute.Endurance)).toBe(20);
		expect(player.attributes.getValue(EAttribute.Agility)).toBe(20);
		expect(player.attributes.getValue(EAttribute.Dexterity)).toBe(20);
		expect(player.attributes.getValue(EAttribute.MaxHealth)).toBe(675);
		expect(player.attributes.getValue(EAttribute.Defense)).toBe(32);
		expect(player.attributes.getValue(EAttribute.CooldownRecovery)).toBe(10);
		expect(player.cdMultiplier).toBeCloseTo(1.1, 10);
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
