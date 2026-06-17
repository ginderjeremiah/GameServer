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

/** The shared battle-RNG seed both simulators construct their Mulberry32 from — mirrors the backend's
 *  ParitySeed. Immaterial to these scenarios (their crit/dodge/block chances are forced to 1/0, so the
 *  outcome never depends on a draw) but both suites must seed identically. */
const PARITY_SEED = 0x9e3779b9;

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
	//   Player: MaxHealth=900, Def=42, CDR=1.09 → cdMult=1.09; damage 85, after def 68.
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
	//   Merged: Str=45, End=20, Agi=20, Dex=20 → MaxHealth=675, Def=32, CDR=1.10 → cdMult=1.10.
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
	//   Player: Str=20, base CDR=1 (cdMult=1); skill = Str×1.0 raw, cooldown 400.
	//     Effect: Self +1.0 CooldownRecovery → cdMult=2 once applied (base 1 + 1), permanent.
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
								1,
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
	//   Player: base CDR=1. slot0 = pure buffer (0 dmg, cooldown 40, Self +1.0 CDR permanent → cdMult=2),
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
								1,
								PERMANENT
							)
						]
					),
					makeSkill(27, 400)
				]
			),
		enemy: () => makeBattler([{ id: EAttribute.Strength, amount: 5 }], []),
		expected: { victory: true, playerDied: false, totalMs: 600 }
	},

	// DoT kill timing: a pure poison (an Opponent DamageTakenPerSecond debuff, no direct damage) ticks the
	// enemy down. DTPS 50 -> 50*40/1000 = 2 damage at the END of each tick, bypassing Defense. 2/tick over
	// 25 ticks brings the 50-HP enemy to 0 -> victory at tick 1000.
	{
		name: 'poisonKillTiming',
		player: () =>
			makeBattler(
				[{ id: EAttribute.Endurance, amount: 0 }],
				[
					makeSkill(
						0,
						40,
						[],
						[
							makeEffect(
								200,
								ESkillEffectTarget.Opponent,
								EAttribute.DamageTakenPerSecond,
								EModifierType.Additive,
								50,
								PERMANENT
							)
						]
					)
				]
			),
		enemy: () => makeBattler([{ id: EAttribute.Endurance, amount: 0 }], []),
		expected: { victory: true, playerDied: false, totalMs: 1000 }
	},

	// Same-tick mutual DoT tie favours the player: both battlers poison each other for 2/tick and would
	// reach 0 HP on the same tick (1000). The end-of-tick phase resolves the ENEMY first, so its death is
	// awarded as a victory before the player's identical DoT is applied -- the player never takes the lethal
	// tick. (Applying the player's DoT first would flip this to a loss.)
	{
		name: 'poisonMutualDotTieFavoursPlayer',
		player: () =>
			makeBattler(
				[{ id: EAttribute.Endurance, amount: 0 }],
				[
					makeSkill(
						0,
						40,
						[],
						[
							makeEffect(
								201,
								ESkillEffectTarget.Opponent,
								EAttribute.DamageTakenPerSecond,
								EModifierType.Additive,
								50,
								PERMANENT
							)
						]
					)
				]
			),
		enemy: () =>
			makeBattler(
				[{ id: EAttribute.Endurance, amount: 0 }],
				[
					makeSkill(
						0,
						40,
						[],
						[
							makeEffect(
								202,
								ESkillEffectTarget.Opponent,
								EAttribute.DamageTakenPerSecond,
								EModifierType.Additive,
								50,
								PERMANENT
							)
						]
					)
				]
			),
		expected: { victory: true, playerDied: false, totalMs: 1000 }
	},

	// Heal-over-time offsets incoming damage and flips the outcome. The player self-applies
	// HealthRegenPerSecond 75 -> 3 HP at each tick's end; the enemy chips 2/tick (baseDamage 4 - Def 2). The
	// regen fully offsets the chip (capped at MaxHealth), so the 50-HP player never dies, and dealing no
	// damage back the battle runs to the timeout. Without the regen the 2/tick would kill the player at 1000.
	{
		name: 'regenOutpacesDamage',
		player: () =>
			makeBattler(
				[{ id: EAttribute.Endurance, amount: 0 }],
				[
					makeSkill(
						0,
						40,
						[],
						[
							makeEffect(
								203,
								ESkillEffectTarget.Self,
								EAttribute.HealthRegenPerSecond,
								EModifierType.Additive,
								75,
								PERMANENT
							)
						]
					)
				]
			),
		enemy: () => makeBattler([{ id: EAttribute.Endurance, amount: 0 }], [makeSkill(4, 40)]),
		expected: { victory: false, playerDied: false, totalMs: DEFAULT_MAX_BATTLE_MS }
	},

	// Ordering within the end-of-tick phase: for each battler DamageTakenPerSecond is applied and death
	// checked BEFORE HealthRegenPerSecond. The player is poisoned for 10/tick and self-heals 6/tick (a net
	// -4 grinding its 50 HP down). On the tick the 10 DoT takes HP from 10 to 0 the player dies -- the 6 heal
	// that would have saved it (10+6-10=6) never runs because death is checked first. Death at tick 11 -> 440.
	{
		name: 'dotBeforeRegenSameTickKills',
		player: () =>
			makeBattler(
				[{ id: EAttribute.Endurance, amount: 0 }],
				[
					makeSkill(
						0,
						40,
						[],
						[
							makeEffect(
								204,
								ESkillEffectTarget.Self,
								EAttribute.HealthRegenPerSecond,
								EModifierType.Additive,
								150,
								PERMANENT
							)
						]
					)
				]
			),
		enemy: () =>
			makeBattler(
				[{ id: EAttribute.Endurance, amount: 0 }],
				[
					makeSkill(
						0,
						40,
						[],
						[
							makeEffect(
								205,
								ESkillEffectTarget.Opponent,
								EAttribute.DamageTakenPerSecond,
								EModifierType.Additive,
								250,
								PERMANENT
							)
						]
					)
				]
			),
		expected: { victory: false, playerDied: true, totalMs: 440 }
	},

	// DoT bypasses Defense. The enemy's high Defense (12) clamps the player's direct 10-damage hit to 0, but
	// the same skill's Opponent DamageTakenPerSecond 250 -> 10/tick ignores Defense and grinds the enemy's
	// 250 HP down: victory at tick 1000. If DoT respected Defense (10-12 clamped to 0) the enemy would never die.
	{
		name: 'dotBypassesDefense',
		player: () =>
			makeBattler(
				[{ id: EAttribute.Endurance, amount: 0 }],
				[
					makeSkill(
						10,
						40,
						[],
						[
							makeEffect(
								206,
								ESkillEffectTarget.Opponent,
								EAttribute.DamageTakenPerSecond,
								EModifierType.Additive,
								250,
								PERMANENT
							)
						]
					)
				]
			),
		enemy: () => makeBattler([{ id: EAttribute.Endurance, amount: 10 }], []),
		expected: { victory: true, playerDied: false, totalMs: 1000 }
	},

	// DoT stops when its effect expires. A single poison application -- the skill's 2000ms cooldown fires it
	// once at tick 50 -- lasts 400ms = 10 ticks, dealing 2/tick for 20 total, leaving the 50-HP enemy at 30.
	// Capped at maxMs 3000 (before the skill could fire again at 4000) the battle ends with no winner; had
	// the DoT never expired the 2/tick would have killed the enemy by ~2960.
	{
		name: 'dotExpiresStopsDamage',
		player: () =>
			makeBattler(
				[{ id: EAttribute.Endurance, amount: 0 }],
				[
					makeSkill(
						0,
						2000,
						[],
						[
							makeEffect(
								207,
								ESkillEffectTarget.Opponent,
								EAttribute.DamageTakenPerSecond,
								EModifierType.Additive,
								50,
								400
							)
						]
					)
				]
			),
		enemy: () => makeBattler([{ id: EAttribute.Endurance, amount: 0 }], []),
		maxMs: 3000,
		expected: { victory: false, playerDied: false, totalMs: 3000 }
	},

	// ── Seeded crit/dodge/block (player-only) ────────────────────────────────────
	// Each chance is forced to 1 or 0 so the outcome is deterministic regardless of the seed (a chance ≥ 1
	// always succeeds against a [0,1) draw, a chance ≤ 0 never does). The draws are still taken in lockstep
	// on both sides. Mirrors the backend `forcedCrit` / `forcedDodge` / `forcedBlock` / `drawOrderMultiSkill`
	// / `enemyForcedChanceIgnored` scenarios.

	// Forced crit: 20 raw × 2 CriticalDamage = 40, −2 def = 38/hit, so the 100-HP enemy dies on hit 3 at 1200ms.
	{
		name: 'forcedCrit',
		player: () =>
			makeBattler(
				[
					{ id: EAttribute.Strength, amount: 10 },
					{ id: EAttribute.CriticalChance, amount: 1 },
					{ id: EAttribute.CriticalDamage, amount: 2 }
				],
				[makeSkill(20, 400)]
			),
		enemy: () => makeBattler([{ id: EAttribute.Strength, amount: 10 }], []),
		expected: { victory: true, playerDied: false, totalMs: 1200 }
	},

	// Forced dodge: the enemy's 1000-damage one-shot is negated every hit, so the player grinds it down
	// (18/hit) for the win on hit 6 at 2400ms instead of dying at tick 10.
	{
		name: 'forcedDodge',
		player: () =>
			makeBattler(
				[
					{ id: EAttribute.Strength, amount: 10 },
					{ id: EAttribute.DodgeChance, amount: 1 }
				],
				[makeSkill(20, 400)]
			),
		enemy: () => makeBattler([{ id: EAttribute.Strength, amount: 10 }], [makeSkill(1000, 400)]),
		expected: { victory: true, playerDied: false, totalMs: 2400 }
	},

	// Forced block: 25 − 2 def − 20 block = 3/hit instead of 23/hit, so the player survives the enemy's
	// every-5-tick assault long enough to kill the 200-HP enemy (48/hit) on hit 5 at 2000ms. Block flips
	// the loss (player would die at tick 25) into a win.
	{
		name: 'forcedBlock',
		player: () =>
			makeBattler(
				[
					{ id: EAttribute.Strength, amount: 10 },
					{ id: EAttribute.BlockChance, amount: 1 },
					{ id: EAttribute.BlockReduction, amount: 20 }
				],
				[makeSkill(50, 400)]
			),
		enemy: () => makeBattler([{ id: EAttribute.Strength, amount: 30 }], [makeSkill(25, 200)]),
		expected: { victory: true, playerDied: false, totalMs: 2000 }
	},

	// Draw-order alignment over a multi-skill exchange: two player skills (two crit draws) and two enemy
	// skills (two dodge+block draw pairs) fire on the same ticks. Player crits both hits (18 + 28 = 46/tick)
	// and blocks both enemy hits (0 + 2 = 2/tick); the 100-HP enemy dies on the player's tick-30 volley → 1200ms.
	{
		name: 'drawOrderMultiSkill',
		player: () =>
			makeBattler(
				[
					{ id: EAttribute.Strength, amount: 10 },
					{ id: EAttribute.CriticalChance, amount: 1 },
					{ id: EAttribute.CriticalDamage, amount: 2 },
					{ id: EAttribute.BlockChance, amount: 1 },
					{ id: EAttribute.BlockReduction, amount: 10 }
				],
				[makeSkill(10, 400), makeSkill(15, 400)]
			),
		enemy: () => makeBattler([{ id: EAttribute.Strength, amount: 10 }], [makeSkill(12, 400), makeSkill(14, 400)]),
		expected: { victory: true, playerDied: false, totalMs: 1200 }
	},

	// Enemies never crit/dodge/block: with every chance forced to 1 on the enemy, its hit still lands
	// un-critted (18, not 38) and the player's hits still land in full (not zeroed by its 50 BlockReduction),
	// so the player wins on hit 6 at 2400ms — proving the rolls are gated on the player alone.
	{
		name: 'enemyForcedChanceIgnored',
		player: () => makeBattler([{ id: EAttribute.Strength, amount: 10 }], [makeSkill(20, 400)]),
		enemy: () =>
			makeBattler(
				[
					{ id: EAttribute.Strength, amount: 10 },
					{ id: EAttribute.CriticalChance, amount: 1 },
					{ id: EAttribute.CriticalDamage, amount: 2 },
					{ id: EAttribute.DodgeChance, amount: 1 },
					{ id: EAttribute.BlockChance, amount: 1 },
					{ id: EAttribute.BlockReduction, amount: 50 }
				],
				[makeSkill(20, 400)]
			),
		expected: { victory: true, playerDied: false, totalMs: 2400 }
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
			const sim = new BattleSimulator(scenario.player(), scenario.enemy(), PARITY_SEED);
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
		expect(player.attributes.getValue(EAttribute.CooldownRecovery)).toBeCloseTo(1.09, 10);
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
		expect(player.attributes.getValue(EAttribute.CooldownRecovery)).toBeCloseTo(1.1, 10);
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
			{ id: EAttribute.CooldownRecovery, amount: 1.09 } // 1 + 0.004*20 + 0.001*10
		];
		const player = makeBattler(playerFinalAttrs, [
			makeSkill(10, 1200, [{ attributeId: EAttribute.Strength, multiplier: 1.5 }])
		]);
		const enemy = scenarios[0].enemy();

		// Derived stats are now DOUBLED.
		expect(player.attributes.getValue(EAttribute.MaxHealth)).toBe(1800);
		expect(player.attributes.getValue(EAttribute.Defense)).toBe(84);
		expect(player.attributes.getValue(EAttribute.CooldownRecovery)).toBeCloseTo(2.18, 10);
		expect(player.cdMultiplier).toBeCloseTo(2.18, 10);

		const result = new BattleSimulator(player, enemy).simulate();
		expect(result.totalMs).toBe(3360);
		expect(result.totalMs).toBeLessThan(6720);
	});
});
