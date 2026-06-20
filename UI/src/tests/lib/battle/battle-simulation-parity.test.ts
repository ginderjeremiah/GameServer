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

	// Fractional enemy attribute distribution (#941): the only scenario whose enemy attribute is a FRACTIONAL
	// value (Strength 7.5, produced on the backend by a 2.5/level distribution at level 3 and serialized to the
	// client, which re-derives MaxHealth from it). Strength feeds only MaxHealth: 50 + 5×7.5 = 87.5, a fractional
	// max with a TIGHT kill margin. Mirrors the backend `fractionalEnemyDistribution` scenario.
	//   Player: skill baseDamage 31, no multiplier, cooldown 400 → 31−2 Def = 29/hit every 10 ticks.
	//   Enemy:  MaxHealth 87.5, Def 2, no skills.
	//   Cumulative 29,58,87 (the 87.5 survives the 87), 116 → dies on hit 4 at tick 160 → 1600ms. Had the .5 been
	//   lost to a float roundtrip (MaxHealth 87) the enemy would die a hit earlier at 1200.
	{
		name: 'fractionalEnemyDistribution',
		player: () => makeBattler([], [makeSkill(31, 400)]),
		enemy: () => makeBattler([{ id: EAttribute.Strength, amount: 7.5 }], []),
		expected: { victory: true, playerDied: false, totalMs: 1600 }
	},

	// A self Strength buff: the carrying hit uses the pre-effect attributes and only subsequent hits see
	// the boost (which also raises damage via the Strength→damage path); re-applying it each fire now STACKS
	// (each fire adds another +10). Mirrors the backend `selfStrengthBuffStacksEachFire` scenario.
	//   Player: Str=10, cdMult=1; skill = Str×1.0 raw, cooldown 400. Effect: Self +10 Strength, permanent.
	//   Enemy:  Str=10 → MaxHealth=100, Def=2, no skills.
	//   Fire 1 deals 10−2=8 then stacks → Str 20; fire 2 deals 18 → Str 30; fire 3 deals 28; fire 4 deals
	//   38; fire 5 deals 48. Enemy HP 100 → 92,74,46,8,−40: dies on fire 5 at 2000. (Refresh-only: 2800.)
	{
		name: 'selfStrengthBuffStacksEachFire',
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
		expected: { victory: true, playerDied: false, totalMs: 2000 }
	},

	// A self CooldownRecovery buff is read live each tick AND stacks on each fire, so the fire interval keeps
	// shortening as the buff compounds. Mirrors the backend `cdrBuffShortensFireInterval` scenario.
	//   Player: Str=20, base CDR=1 (cdMult=1); skill = Str×1.0 raw, cooldown 400. Each hit deals 18.
	//     Effect: Self +1.0 CooldownRecovery (additive), permanent — a stack per fire.
	//   Enemy:  Str=10 → MaxHealth=100, Def=2, no skills.
	//   Fire 1 at tick 10 → cdMult=2; next fires 5,4,3,2,2 ticks apart (ticks 15,19,22,24,26). Six 18-dmg
	//   hits drop the 100-HP enemy on fire 6 at tick 26 → 1040ms. (Refresh-only held cdMult=2 → 1400.)
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
		expected: { victory: true, playerDied: false, totalMs: 1040 }
	},

	// Same-tick ordering: slot 0's self buff (applied after its own pre-buff hit) influences slot 1 firing
	// after it on the same tick; the buff also STACKS on each fire, so the per-volley damage ramps. Mirrors
	// the backend `sameTickEarlierSlotBuffsLaterSlot` scenario.
	//   Player: Str=10; slot0 = Str×1.0 (Self +10 Str, permanent — a stack per fire), slot1 = Str×1.0 (no
	//     effect), both cooldown 400 (fire ticks 10,20,30…). Enemy: Str=16 → MaxHealth=130, Def=2, no skills.
	//   Tick 10: 8 (slot0) + 18 (slot1, Str 20) = 26; tick 20: 18 + 28 = 46; tick 30: 28 + 38 → enemy dies at 1200.
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
		expected: { victory: true, playerDied: false, totalMs: 1200 }
	},

	// MaxHealth clamp: an Opponent MaxHealth ×0.5 debuff halves the enemy's maximum and clamps its current
	// health down to it; the debuff also STACKS on each fire, so MaxHealth keeps halving. Mirrors the backend
	// `opponentMaxHealthDebuffClamps` scenario.
	//   Player: skill baseDamage 12, no multiplier → 12−2 = 10 per hit, cooldown 400 (fires tick 10,20…).
	//     Effect: Opponent ×0.5 MaxHealth (multiplicative), permanent — a stack per fire.
	//   Enemy:  Str=10 → MaxHealth=100, Def=2, no skills.
	//   Tick 10: 100→90, ×0.5 → 50 (clamp 90→50); tick 20: →40, ×0.5 → 25; tick 30: →15, ×0.5 → 12.5;
	//   tick 40: →2.5, ×0.5 → 6.25; tick 50: →dead at 2000ms.
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
		expected: { victory: true, playerDied: false, totalMs: 2000 }
	},

	// Same-tick CDR ordering: slot 0's self CooldownRecovery buff speeds up slot 1's accrual on the same
	// tick, because each slot reads the cooldown multiplier live in loadout order; slot 0 fires every tick
	// and its buff STACKS, so cdMult climbs 1→2→3→… Mirrors the backend `cdrBuffSpeedsLaterSlotSameTick`.
	//   Player: base CDR=1. slot0 = pure buffer (0 dmg, cooldown 40, Self +1.0 CDR permanent — a stack per
	//     tick), slot1 = baseDamage 27, cooldown 400. Enemy Str=5 → MaxHealth=75, Def=2, no skills (25/hit).
	//   slot1 accrues 80,120,160,200,… so its charge passes 400 at ticks 4, 6, 8 → 3 hits kill at tick 8 → 320ms.
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
		expected: { victory: true, playerDied: false, totalMs: 320 }
	},

	// DoT kill timing: a constant poison on the enemy (a base DamageTakenPerSecond debuff, no direct damage)
	// ticks it down. DTPS 50 -> 50*40/1000 = 2 damage at the END of each tick, bypassing Defense. 2/tick over
	// 25 ticks brings the 50-HP enemy to 0 -> victory at tick 1000.
	{
		name: 'poisonKillTiming',
		player: () => makeBattler([{ id: EAttribute.Endurance, amount: 0 }], []),
		enemy: () =>
			makeBattler(
				[
					{ id: EAttribute.Endurance, amount: 0 },
					{ id: EAttribute.DamageTakenPerSecond, amount: 50 }
				],
				[]
			),
		expected: { victory: true, playerDied: false, totalMs: 1000 }
	},

	// Same-tick mutual DoT tie favours the player: both battlers carry a constant DamageTakenPerSecond (base
	// poison) for 2/tick and reach 0 HP on the same tick (1000). The end-of-tick phase resolves the ENEMY
	// first, so its death is awarded as a victory before the player's identical DoT is applied -- the player
	// never takes the lethal tick. (Applying the player's DoT first would flip this to a loss.)
	{
		name: 'poisonMutualDotTieFavoursPlayer',
		player: () =>
			makeBattler(
				[
					{ id: EAttribute.Endurance, amount: 0 },
					{ id: EAttribute.DamageTakenPerSecond, amount: 50 }
				],
				[]
			),
		enemy: () =>
			makeBattler(
				[
					{ id: EAttribute.Endurance, amount: 0 },
					{ id: EAttribute.DamageTakenPerSecond, amount: 50 }
				],
				[]
			),
		expected: { victory: true, playerDied: false, totalMs: 1000 }
	},

	// Heal-over-time offsets incoming damage and flips the outcome. The player carries a constant
	// HealthRegenPerSecond 75 (base regen) -> 3 HP at each tick's end; the enemy chips 2/tick (baseDamage 4 -
	// Def 2). The regen fully offsets the chip (capped at MaxHealth), so the 50-HP player never dies, and
	// dealing no damage back the battle runs to the timeout. Without the regen the 2/tick would kill at 1000.
	{
		name: 'regenOutpacesDamage',
		player: () =>
			makeBattler(
				[
					{ id: EAttribute.Endurance, amount: 0 },
					{ id: EAttribute.HealthRegenPerSecond, amount: 75 }
				],
				[]
			),
		enemy: () => makeBattler([{ id: EAttribute.Endurance, amount: 0 }], [makeSkill(4, 40)]),
		expected: { victory: false, playerDied: false, totalMs: DEFAULT_MAX_BATTLE_MS }
	},

	// Ordering within the end-of-tick phase: for each battler DamageTakenPerSecond is applied and death
	// checked BEFORE HealthRegenPerSecond. The player carries a constant 250 DamageTakenPerSecond (10/tick)
	// and 150 HealthRegenPerSecond (6/tick) -- a net -4 grinding its 50 HP down. On the tick the 10 DoT takes
	// HP from 10 to 0 the player dies -- the 6 heal that would have saved it never runs because death is
	// checked first. Death at tick 11 -> 440.
	{
		name: 'dotBeforeRegenSameTickKills',
		player: () =>
			makeBattler(
				[
					{ id: EAttribute.Endurance, amount: 0 },
					{ id: EAttribute.DamageTakenPerSecond, amount: 250 },
					{ id: EAttribute.HealthRegenPerSecond, amount: 150 }
				],
				[]
			),
		enemy: () => makeBattler([{ id: EAttribute.Endurance, amount: 0 }], []),
		expected: { victory: false, playerDied: true, totalMs: 440 }
	},

	// DoT bypasses Defense. The enemy's high Defense (12) clamps the player's direct 10-damage hit to 0, but
	// its constant 250 DamageTakenPerSecond (base poison) -> 10/tick ignores Defense and grinds the enemy's
	// 250 HP down: victory at tick 1000. If DoT respected Defense (10-12 clamped to 0) the enemy would never die.
	{
		name: 'dotBypassesDefense',
		player: () => makeBattler([{ id: EAttribute.Endurance, amount: 0 }], [makeSkill(10, 40)]),
		enemy: () =>
			makeBattler(
				[
					{ id: EAttribute.Endurance, amount: 10 },
					{ id: EAttribute.DamageTakenPerSecond, amount: 250 }
				],
				[]
			),
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

	// Effect apply → expire → re-apply (#941): a periodic poison whose duration (80ms = 2 ticks) is shorter than
	// its skill's cooldown (120ms = 3 ticks), so each application fully LAPSES for a tick before re-applying — the
	// cycle where the frontend's decrementing remainingMs and the backend's absolute ExpiresAtMs clock are most
	// likely to drift by a tick. Mirrors the backend `effectExpiresThenReapplies` scenario.
	//   Player: skill baseDamage 0, cooldown 120, Opponent +250 DamageTakenPerSecond for 80ms → 10/tick. Fires at
	//     ticks 120,240,360. Enemy: MaxHealth 50, no skills.
	//   Active 120,160 (50→30), lapse 200, re-apply 240,280 (30→10), lapse 320, re-apply 360 (→0): dies at 360ms.
	//   (A non-lapsing constant 10/tick from tick 120 would kill at 280.)
	{
		name: 'effectExpiresThenReapplies',
		player: () =>
			makeBattler(
				[],
				[
					makeSkill(
						0,
						120,
						[],
						[
							makeEffect(
								210,
								ESkillEffectTarget.Opponent,
								EAttribute.DamageTakenPerSecond,
								EModifierType.Additive,
								250,
								80
							)
						]
					)
				]
			),
		enemy: () => makeBattler([], []),
		expected: { victory: true, playerDied: false, totalMs: 360 }
	},

	// DoT stacking: a poison re-applied every tick STACKS (each application adds another
	// DamageTakenPerSecond debuff) instead of refreshing, so the per-tick damage ramps. The skill
	// (cooldown 40) applies Opponent +25 DamageTakenPerSecond each tick; at tick n the enemy carries n
	// stacks = 25n DTPS → 25n×40/1000 = n damage that tick (bypassing Defense). Cumulative n(n+1)/2 reaches
	// the 50-HP enemy's total on tick 10 (45 after tick 9, +10 = 55) → victory at 400ms. (Refresh-only would
	// hold 25 DTPS = 1/tick → 2000ms.) Mirrors the backend `dotStacksEachApplication` scenario.
	{
		name: 'dotStacksEachApplication',
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
								208,
								ESkillEffectTarget.Opponent,
								EAttribute.DamageTakenPerSecond,
								EModifierType.Additive,
								25,
								PERMANENT
							)
						]
					)
				]
			),
		enemy: () => makeBattler([{ id: EAttribute.Endurance, amount: 0 }], []),
		expected: { victory: true, playerDied: false, totalMs: 400 }
	},

	// Effect magnitude scales with the CASTER's attribute (#741): a poison whose authored amount is 0 but
	// scales with the caster's Intellect at 1.0/point. Intellect feeds no derived attribute, so it perturbs
	// nothing but this scaling. The skill (baseDamage 0, cooldown 2000) fires once at tick 50, applying
	// Opponent +DamageTakenPerSecond = 0 + Intellect(50)×1.0 = 50 DTPS (permanent). 50 DTPS → 2/tick
	// (bypassing Defense); from tick 50 the 50-HP enemy loses 2/tick and dies on tick 74 → 2960ms. (Without
	// scaling the authored 0 deals nothing.) Mirrors the backend `effectScalesWithCasterIntellect` scenario.
	{
		name: 'effectScalesWithCasterIntellect',
		player: () =>
			makeBattler(
				[{ id: EAttribute.Intellect, amount: 50 }],
				[
					makeSkill(
						0,
						2000,
						[],
						[
							makeEffect(
								209,
								ESkillEffectTarget.Opponent,
								EAttribute.DamageTakenPerSecond,
								EModifierType.Additive,
								0,
								PERMANENT,
								EAttribute.Intellect,
								1.0
							)
						]
					)
				]
			),
		enemy: () => makeBattler([{ id: EAttribute.Endurance, amount: 0 }], []),
		expected: { victory: true, playerDied: false, totalMs: 2960 }
	},

	// ── Seeded crit/dodge/block (player-only) ────────────────────────────────────
	// Each chance is forced to 1 or 0 so the outcome is deterministic regardless of the seed (a chance ≥ 1
	// always succeeds against a [0,1) draw, a chance ≤ 0 never does). The draws are still taken in lockstep
	// on both sides. Mirrors the backend `forcedCrit` / `forcedDodge` / `forcedBlock` / `drawOrderMultiSkill`
	// / `enemyForcedChanceIgnored` scenarios.

	// Forced crit: CriticalDamage is the base 1.5 (sourced by #799) + 0.5 = 2, so 20 raw × 2 = 40, −2 def =
	// 38/hit, and the 100-HP enemy dies on hit 3 at 1200ms.
	{
		name: 'forcedCrit',
		player: () =>
			makeBattler(
				[
					{ id: EAttribute.Strength, amount: 10 },
					{ id: EAttribute.CriticalChance, amount: 1 },
					{ id: EAttribute.CriticalDamage, amount: 0.5 }
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

	// Forced block: BlockReduction is the base 2 (sourced by #799) + 18 = 20, so 25 − 2 def − 20 block =
	// 3/hit instead of 23/hit, and the player survives the enemy's every-5-tick assault long enough to kill
	// the 200-HP enemy (48/hit) on hit 5 at 2000ms. Block flips the loss (player would die at tick 25) into a win.
	{
		name: 'forcedBlock',
		player: () =>
			makeBattler(
				[
					{ id: EAttribute.Strength, amount: 10 },
					{ id: EAttribute.BlockChance, amount: 1 },
					{ id: EAttribute.BlockReduction, amount: 18 }
				],
				[makeSkill(50, 400)]
			),
		enemy: () => makeBattler([{ id: EAttribute.Strength, amount: 30 }], [makeSkill(25, 200)]),
		expected: { victory: true, playerDied: false, totalMs: 2000 }
	},

	// Draw-order alignment over a multi-skill exchange: two player skills (two crit draws) and two enemy
	// skills (two dodge+block draw pairs) fire on the same ticks. CriticalDamage and BlockReduction each fold
	// in the #799 base (1.5 + 0.5 = 2 multiplier; 2 + 8 = 10 reduction). Player crits both hits (18 + 28 =
	// 46/tick) and blocks both enemy hits (0 + 2 = 2/tick); the 100-HP enemy dies on the player's tick-30 volley → 1200ms.
	{
		name: 'drawOrderMultiSkill',
		player: () =>
			makeBattler(
				[
					{ id: EAttribute.Strength, amount: 10 },
					{ id: EAttribute.CriticalChance, amount: 1 },
					{ id: EAttribute.CriticalDamage, amount: 0.5 },
					{ id: EAttribute.BlockChance, amount: 1 },
					{ id: EAttribute.BlockReduction, amount: 8 }
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
	},

	// Fractional crit chance against a fixed seed (#941): unlike the forced-1/0 crit rows, CriticalChance is a
	// real 0.5 fraction, so whether each player fire crits depends on the actual Mulberry32 [0,1) draw. With
	// PARITY_SEED the first six crit draws are crit,crit,no,no,crit,crit. Mirrors the backend `fractionalCritChance`.
	//   Player: skill baseDamage 12, cooldown 400 (one crit draw per fire); CriticalDamage base 1.5 + 0.5 = 2.0 →
	//     a crit deals 12×2−2 = 22, a non-crit 12−2 = 10. Enemy: Str 10 → MaxHealth 100, Def 2, no skills.
	//   Hits 22,22,10,10,22 (cum 86) then the 6th draw's crit (+22 → 108 ≥ 100) kills on fire 6 → 2400ms; a
	//   non-crit there (96) would push the kill to fire 7.
	{
		name: 'fractionalCritChance',
		player: () =>
			makeBattler(
				[
					{ id: EAttribute.CriticalChance, amount: 0.5 },
					{ id: EAttribute.CriticalDamage, amount: 0.5 }
				],
				[makeSkill(12, 400)]
			),
		enemy: () => makeBattler([{ id: EAttribute.Strength, amount: 10 }], []),
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

	it('derives the fractionalEnemyDistribution enemy MaxHealth to the expected fractional value', () => {
		// Pins the fractional enemy stat down so a divergence in the fractional value (the backend's
		// decimal→double per-level distribution, or the wire roundtrip this side mirrors) is reported
		// directly rather than only as a different outcome. Mirrors
		// BattleSimulatorParityTests.Parity_FractionalEnemyDistribution_DerivesFractionalMaxHealth.
		const scenario = scenarios.find((s) => s.name === 'fractionalEnemyDistribution');
		if (!scenario) {
			throw new Error('fractionalEnemyDistribution scenario is missing from the matrix');
		}
		const enemy = scenario.enemy();

		// Strength 7.5 (the backend's 2.5/level distribution at level 3) → MaxHealth 50 + 5×7.5 = 87.5.
		expect(enemy.attributes.getValue(EAttribute.Strength)).toBe(7.5);
		expect(enemy.attributes.getValue(EAttribute.MaxHealth)).toBe(87.5);
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

		const result = new BattleSimulator(player, enemy, PARITY_SEED).simulate();
		expect(result.totalMs).toBe(3360);
		expect(result.totalMs).toBeLessThan(6720);
	});
});
