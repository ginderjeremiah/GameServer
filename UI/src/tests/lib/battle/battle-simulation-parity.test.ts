import { describe, it, expect, beforeEach, vi } from 'vitest';
import { EAttribute, EDamageType, EItemModType, EModifierType, ESkillEffectTarget } from '$lib/api';
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
import {
	battlerFactory,
	equipmentFactory,
	grantedBattlerFactory,
	makeSkill,
	makeMultiTypeSkill,
	makeEffect
} from './battle-sim-test-utils';

/** A duration long enough that an effect never expires within a battle (for "permanent" buffs). */
const PERMANENT = 1_000_000;

/** The shared battle-RNG seed both simulators construct their Mulberry32 from — mirrors the backend's
 *  ParitySeed. Most scenarios force their crit/dodge chances to 1/0 (outcome independent of a draw), but
 *  the fractional-crit rows DO depend on the exact stream, so both suites must seed identically. */
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
const granted = grantedBattlerFactory(mockSkills);

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
	//   Player: MaxHealth=900, Toughness=60, CDR=1.09 → cdMult=1.09; damage 85 → enemy Toughness 30 → 34/hit.
	//   Enemy:  MaxHealth=400, Toughness=30; damage 5 → player Toughness 60 → 1.25/hit. 12 hits → 13440ms.
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
		expected: { victory: true, playerDied: false, totalMs: 13440 }
	},

	// Two player skills on different cooldowns vs an enemy that deals real damage.
	//   Player: MaxHealth=600, Toughness=40; skillA 50 raw→20/hit every 20 ticks, skillB 25 raw→10/hit every 30 ticks.
	//   Enemy:  MaxHealth=450, Toughness=30; attack 30 raw→player Toughness 40→10/hit every 25 ticks. Kill at tick 340 → 13600ms.
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
		expected: { victory: true, playerDied: false, totalMs: 13600 }
	},

	// Both sides have so much Toughness (Endurance 50 → Toughness 100) that each 5-damage hit is reduced to a
	// trickle (5 × 20/(100+20) ≈ 0.83), and with their Endurance-inflated MaxHealth (1050 each) that trickle
	// cannot kill within the cap — a draw. The curve never clamps a hit fully to 0 (it asymptotes below 100%),
	// so the stalemate now comes from EHP outpacing the trickle rather than a hard floor.
	{
		name: 'highToughnessTrickle',
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
	// against a player who out-tanks it. Mirrors the backend `bossFullLoadout` scenario. Under the curve the
	// boss's smaller skills no longer clamp to 0 — they all chip — so the player is built tankier to still win.
	//   Player: Str=60, End=50 → MaxHealth=1350, Toughness=100; skill 130 raw → boss Toughness 60 → 32.5/hit, every 25 ticks.
	//   Boss:   Str=20, End=30 → MaxHealth=750, Toughness=60; 3 skills → 8.33 + 3.33 + 5 = 16.67/volley every 25 ticks.
	//   24 player hits reach 780 ≥ 750 at tick 600 → 24000ms; the boss's chip never threatens the 1350-HP player.
	{
		name: 'bossFullLoadout',
		player: () =>
			makeBattler(
				[
					{ id: EAttribute.Strength, amount: 60 },
					{ id: EAttribute.Endurance, amount: 50 }
				],
				[makeSkill(10, 1000, [{ attributeId: EAttribute.Strength, multiplier: 2.0 }])]
			),
		enemy: () =>
			makeBattler(
				[
					{ id: EAttribute.Strength, amount: 20 },
					{ id: EAttribute.Endurance, amount: 30 }
				],
				[makeSkill(50, 1000), makeSkill(20, 1000), makeSkill(30, 1000)]
			),
		expected: { victory: true, playerDied: false, totalMs: 24000 }
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
	//   Merged: Str=45, End=20, Agi=20, Dex=20 → MaxHealth=675, Toughness=40, CDR=1.10 → cdMult=1.10.
	//   Player skill: 10 + 45*1.5 = 77.5 raw → enemy Toughness 30 → 31/hit, fires every 28 ticks
	//     (charge/tick = 40*1.10 = 44, 44*28=1232 ≥ 1200).
	//   Enemy: MaxHealth=400, Toughness=30; attack 5 → 1.67/hit, so the player never dies. 13 hits → 14560ms.
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
		expected: { victory: true, playerDied: false, totalMs: 14560 }
	},

	// Fractional enemy attribute distribution (#941): the only scenario whose enemy attribute is a FRACTIONAL
	// value (Strength 7.5, produced on the backend by a 2.5/level distribution at level 3 and serialized to the
	// client, which re-derives MaxHealth from it). Strength feeds only MaxHealth: 50 + 5×7.5 = 87.5, a fractional
	// max with a TIGHT kill margin. Mirrors the backend `fractionalEnemyDistribution` scenario.
	//   Player: skill baseDamage 29, no multiplier, cooldown 400 → 29/hit (the enemy has no Toughness) every 10 ticks.
	//   Enemy:  MaxHealth 87.5, no Toughness, no skills.
	//   Cumulative 29,58,87 (the 87.5 survives the 87), 116 → dies on hit 4 at tick 40 → 1600ms. Had the .5 been
	//   lost to a float roundtrip (MaxHealth 87) the enemy would die a hit earlier at 1200.
	{
		name: 'fractionalEnemyDistribution',
		player: () => makeBattler([], [makeSkill(29, 400)]),
		enemy: () => makeBattler([{ id: EAttribute.Strength, amount: 7.5 }], []),
		expected: { victory: true, playerDied: false, totalMs: 1600 }
	},

	// A self Strength buff: the carrying hit uses the pre-effect attributes and only subsequent hits see
	// the boost (which also raises damage via the Strength→damage path); re-applying it each fire now STACKS
	// (each fire adds another +10). Mirrors the backend `selfStrengthBuffStacksEachFire` scenario.
	//   Player: Str=10, cdMult=1; skill = Str×1.0 raw, cooldown 400. Effect: Self +10 Strength, permanent.
	//   Enemy:  Str=10 → MaxHealth=100, no Toughness, no skills.
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
		expected: { victory: true, playerDied: false, totalMs: 1600 }
	},

	// A self CooldownRecovery buff is read live each tick AND stacks on each fire, so the fire interval keeps
	// shortening as the buff compounds. Mirrors the backend `cdrBuffShortensFireInterval` scenario.
	//   Player: Str=20, base CDR=1 (cdMult=1); skill = Str×1.0 raw, cooldown 400. Each hit deals 18.
	//     Effect: Self +1.0 CooldownRecovery (additive), permanent — a stack per fire.
	//   Enemy:  Str=10 → MaxHealth=100, no Toughness, no skills.
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
		expected: { victory: true, playerDied: false, totalMs: 960 }
	},

	// Same-tick ordering: slot 0's self buff (applied after its own pre-buff hit) influences slot 1 firing
	// after it on the same tick; the buff also STACKS on each fire, so the per-volley damage ramps. Mirrors
	// the backend `sameTickEarlierSlotBuffsLaterSlot` scenario.
	//   Player: Str=10; slot0 = Str×1.0 (Self +10 Str, permanent — a stack per fire), slot1 = Str×1.0 (no
	//     effect), both cooldown 400 (fire ticks 10,20,30…). Enemy: Str=16 → MaxHealth=130, no Toughness, no skills.
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
	//   Enemy:  Str=10 → MaxHealth=100, no Toughness, no skills.
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

	// MaxHealth debuff is LETHAL via the clamp (#1145): an Opponent MaxHealth additive −200 debuff drives the
	// enemy's maximum below zero, so the clamp pulls its current health to ≤ 0 and the live isDead getter
	// reports death — a kill with NO direct damage, exercising the clamp path a cached isDead flag (re-synced
	// only at the damage mutations) would miss. Mirrors the backend `maxHealthDebuffIsLethal` scenario.
	//   Player: skill baseDamage 0, no multiplier, cooldown 400 (fires tick 10). Effect: Opponent −200 MaxHealth
	//     (additive), permanent.
	//   Enemy:  Str=10 → MaxHealth=100, no Toughness, no skills.
	//   Tick 10: 0 direct damage, then −200 MaxHealth → MaxHealth −100, clamp 100→−100 → dead at 400ms.
	{
		name: 'maxHealthDebuffIsLethal',
		player: () =>
			makeBattler(
				[],
				[
					makeSkill(
						0,
						400,
						[],
						[
							makeEffect(
								105,
								ESkillEffectTarget.Opponent,
								EAttribute.MaxHealth,
								EModifierType.Additive,
								-200,
								PERMANENT
							)
						]
					)
				]
			),
		enemy: () => makeBattler([{ id: EAttribute.Strength, amount: 10 }], []),
		expected: { victory: true, playerDied: false, totalMs: 400 }
	},

	// Same-tick CDR ordering: slot 0's self CooldownRecovery buff speeds up slot 1's accrual on the same
	// tick, because each slot reads the cooldown multiplier live in loadout order; slot 0 fires every tick
	// and its buff STACKS, so cdMult climbs 1→2→3→… Mirrors the backend `cdrBuffSpeedsLaterSlotSameTick`.
	//   Player: base CDR=1. slot0 = pure buffer (0 dmg, cooldown 40, Self +1.0 CDR permanent — a stack per
	//     tick), slot1 = baseDamage 27, cooldown 400. Enemy Str=5 → MaxHealth=75, no Toughness, no skills (25/hit).
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

	// DoT kill timing: a constant poison on the enemy (a base PoisonDamagePerSecond debuff, no direct damage)
	// ticks it down. DTPS 50 -> 50*40/1000 = 2 damage at the END of each tick, bypassing mitigation. 2/tick over
	// 25 ticks brings the 50-HP enemy to 0 -> victory at tick 1000.
	{
		name: 'poisonKillTiming',
		player: () => makeBattler([{ id: EAttribute.Endurance, amount: 0 }], []),
		enemy: () =>
			makeBattler(
				[
					{ id: EAttribute.Endurance, amount: 0 },
					{ id: EAttribute.PoisonDamagePerSecond, amount: 50 }
				],
				[]
			),
		expected: { victory: true, playerDied: false, totalMs: 1000 }
	},

	// Same-tick mutual DoT tie favours the player: both battlers carry a constant PoisonDamagePerSecond (base
	// poison) for 2/tick and reach 0 HP on the same tick (1000). The end-of-tick phase resolves the ENEMY
	// first, so its death is awarded as a victory before the player's identical DoT is applied -- the player
	// never takes the lethal tick. (Applying the player's DoT first would flip this to a loss.)
	{
		name: 'poisonMutualDotTieFavoursPlayer',
		player: () =>
			makeBattler(
				[
					{ id: EAttribute.Endurance, amount: 0 },
					{ id: EAttribute.PoisonDamagePerSecond, amount: 50 }
				],
				[]
			),
		enemy: () =>
			makeBattler(
				[
					{ id: EAttribute.Endurance, amount: 0 },
					{ id: EAttribute.PoisonDamagePerSecond, amount: 50 }
				],
				[]
			),
		expected: { victory: true, playerDied: false, totalMs: 1000 }
	},

	// Heal-over-time offsets incoming damage and flips the outcome. The player carries a constant
	// HealthRegenPerSecond 75 (base regen) -> 3 HP at each tick's end; the enemy chips 2/tick (baseDamage 2, the
	// player has no Toughness). The regen fully offsets the chip (capped at MaxHealth), so the 50-HP player never
	// dies, and dealing no damage back the battle runs to the timeout. Without the regen the 2/tick would kill at 1000.
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
		enemy: () => makeBattler([{ id: EAttribute.Endurance, amount: 0 }], [makeSkill(2, 40)]),
		expected: { victory: false, playerDied: false, totalMs: DEFAULT_MAX_BATTLE_MS }
	},

	// Ordering within the end-of-tick phase: for each battler PoisonDamagePerSecond then HealthRegenPerSecond
	// apply BEFORE its death check, so the heal can offset a lethal DoT tick (#1090). The player carries a
	// constant 250 PoisonDamagePerSecond (10/tick) and 150 HealthRegenPerSecond (6/tick) -- a net -4 grinding
	// its 50 HP down. Each tick the 6 heal applies after the 10 DoT and is checked together, so the player only
	// dies once the net drain crosses 0: 50/4 = 12.5 -> death at tick 13 -> 520. (Checking death before the
	// heal would have killed it at tick 11/440; see regenSavesFromLethalDot for a net-zero drain it survives.)
	{
		name: 'dotRegenNetNegativeKillsAfterHeal',
		player: () =>
			makeBattler(
				[
					{ id: EAttribute.Endurance, amount: 0 },
					{ id: EAttribute.PoisonDamagePerSecond, amount: 250 },
					{ id: EAttribute.HealthRegenPerSecond, amount: 150 }
				],
				[]
			),
		enemy: () => makeBattler([{ id: EAttribute.Endurance, amount: 0 }], []),
		expected: { victory: false, playerDied: true, totalMs: 520 }
	},

	// Heal-over-time saves the battler from an otherwise-lethal DoT tick (#1090). The player carries a constant
	// 1500 PoisonDamagePerSecond (60/tick) -- more than its whole 50 HP -- and an equal 1500 HealthRegenPerSecond
	// (60/tick). Each tick the 60 DoT drives HP to -10 but the same-tick 60 heal (applied before the death
	// check, capped at MaxHealth) restores it to 50, so the player never dies; dealing no damage back, the
	// battle runs to the timeout. (Checking death before the heal would have killed it on tick 1.)
	{
		name: 'regenSavesFromLethalDot',
		player: () =>
			makeBattler(
				[
					{ id: EAttribute.Endurance, amount: 0 },
					{ id: EAttribute.PoisonDamagePerSecond, amount: 1500 },
					{ id: EAttribute.HealthRegenPerSecond, amount: 1500 }
				],
				[]
			),
		enemy: () => makeBattler([{ id: EAttribute.Endurance, amount: 0 }], []),
		expected: { victory: false, playerDied: false, totalMs: DEFAULT_MAX_BATTLE_MS }
	},

	// DoT bypasses the Toughness curve. The enemy's Endurance 10 → Toughness 20 would halve a direct hit against
	// the level-1 player, but its constant 250 PoisonDamagePerSecond (base poison) -> 10/tick ignores mitigation
	// and grinds the enemy's 250 HP down: victory at tick 1000. If DoT were mitigated (5/tick) it would die at 2000.
	{
		name: 'dotBypassesMitigation',
		player: () => makeBattler([{ id: EAttribute.Endurance, amount: 0 }], [makeSkill(0, 40)]),
		enemy: () =>
			makeBattler(
				[
					{ id: EAttribute.Endurance, amount: 10 },
					{ id: EAttribute.PoisonDamagePerSecond, amount: 250 }
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
								EAttribute.PoisonDamagePerSecond,
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
	//   Player: skill baseDamage 0, cooldown 120, Opponent +250 PoisonDamagePerSecond for 80ms → 10/tick. Fires at
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
								EAttribute.PoisonDamagePerSecond,
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
	// PoisonDamagePerSecond debuff) instead of refreshing, so the per-tick damage ramps. The skill
	// (cooldown 40) applies Opponent +25 PoisonDamagePerSecond each tick; at tick n the enemy carries n
	// stacks = 25n DTPS → 25n×40/1000 = n damage that tick (bypassing mitigation). Cumulative n(n+1)/2 reaches
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
								EAttribute.PoisonDamagePerSecond,
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

	// Shared per-attribute expiry (#992 / #740): a poison whose duration (120ms = 3 ticks) outlasts its skill's
	// cooldown (80ms = 2 ticks), so each fire re-applies WHILE the previous applications are still active —
	// resetting the whole PoisonDamagePerSecond stack's single shared expiry to the new duration. The reset
	// always lands before the 3-tick duration would lapse, so nothing ever expires and the stacks accumulate,
	// the damage ramping like a permanent poison. Mirrors the backend `dotReapplyExtendsSharedExpiration`.
	//   Player: skill baseDamage 0, cooldown 80 → fires ticks 2,4,6; Opponent +250 PoisonDamagePerSecond, 120ms.
	//   Enemy:  Str=14 → MaxHealth=120, no Toughness, no skills. Each stack = 250 DTPS → 10/tick (bypassing mitigation).
	//   Stacks 1 (ticks 2-3), 2 (4-5), 3 (6-7); cum 10,20,40,60,90,120 → the 120-HP enemy dies on tick 7 →
	//   280ms. (Independent per-application expiry — the bug this fixes — would lapse older applications and kill at 400ms.)
	{
		name: 'dotReapplyExtendsSharedExpiration',
		player: () =>
			makeBattler(
				[{ id: EAttribute.Endurance, amount: 0 }],
				[
					makeSkill(
						0,
						80,
						[],
						[
							makeEffect(
								211,
								ESkillEffectTarget.Opponent,
								EAttribute.PoisonDamagePerSecond,
								EModifierType.Additive,
								250,
								120
							)
						]
					)
				]
			),
		enemy: () => makeBattler([{ id: EAttribute.Strength, amount: 14 }], []),
		expected: { victory: true, playerDied: false, totalMs: 280 }
	},

	// Effect magnitude scales with the CASTER's attribute (#741): a poison whose authored amount is 0 but
	// scales with the caster's Intellect at 1.0/point. Intellect feeds no derived attribute, so it perturbs
	// nothing but this scaling. The skill (baseDamage 0, cooldown 2000) fires once at tick 50, applying
	// Opponent +PoisonDamagePerSecond = 0 + Intellect(50)×1.0 = 50 DTPS (permanent). 50 DTPS → 2/tick
	// (bypassing mitigation); from tick 50 the 50-HP enemy loses 2/tick and dies on tick 74 → 2960ms. (Without
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
								EAttribute.PoisonDamagePerSecond,
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

	// ── Seeded crit/dodge (player-only) ──────────────────────────────────────────
	// Each chance is forced to 1 or 0 so the outcome is deterministic regardless of the seed (a chance ≥ 1
	// always succeeds against a [0,1) draw, a chance ≤ 0 never does). The draws are still taken in lockstep
	// on both sides. Mirrors the backend `forcedCrit` / `forcedDodge` / `drawOrderMultiSkill` /
	// `enemyForcedChanceIgnored` scenarios.

	// Forced crit: CriticalDamage is the base 1.5 (sourced by #799) + 0.5 = 2, so 20 raw × 2 = 40/hit (the
	// enemy has no Toughness), and the 100-HP enemy dies on hit 3 at 1200ms.
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
	// (20/hit, no Toughness) for the win on hit 5 at 2000ms instead of dying at tick 10.
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
		expected: { victory: true, playerDied: false, totalMs: 2000 }
	},

	// Draw-order alignment over a multi-skill exchange: two player skills (two crit draws) and two enemy
	// skills (two dodge draws, one each now that Block's second draw is gone) fire on the same ticks.
	// CriticalDamage folds in the #799 base (1.5 + 0.5 = 2 multiplier). Neither side has Toughness. The player
	// crits both hits (10×2=20, 15×2=30 → 50/tick); the enemy's two hits land in full (12 + 14 = 26/tick — no
	// Block). The 100-HP enemy dies on the player's tick-20 volley → 800ms (before its own tick-20 attack), so
	// the player only takes the tick-10 volley and ends at 74 HP.
	{
		name: 'drawOrderMultiSkill',
		player: () =>
			makeBattler(
				[
					{ id: EAttribute.Strength, amount: 10 },
					{ id: EAttribute.CriticalChance, amount: 1 },
					{ id: EAttribute.CriticalDamage, amount: 0.5 }
				],
				[makeSkill(10, 400), makeSkill(15, 400)]
			),
		enemy: () => makeBattler([{ id: EAttribute.Strength, amount: 10 }], [makeSkill(12, 400), makeSkill(14, 400)]),
		expected: { victory: true, playerDied: false, totalMs: 800 }
	},

	// Enemies never crit/dodge: with every chance forced to 1 on the enemy, its hit still lands un-critted (20,
	// not 40) and its dodge is irrelevant on offence, so the player's hits land in full and win on hit 5 at
	// 2000ms — proving the rolls are gated on the player alone.
	{
		name: 'enemyForcedChanceIgnored',
		player: () => makeBattler([{ id: EAttribute.Strength, amount: 10 }], [makeSkill(20, 400)]),
		enemy: () =>
			makeBattler(
				[
					{ id: EAttribute.Strength, amount: 10 },
					{ id: EAttribute.CriticalChance, amount: 1 },
					{ id: EAttribute.CriticalDamage, amount: 2 },
					{ id: EAttribute.DodgeChance, amount: 1 }
				],
				[makeSkill(20, 400)]
			),
		expected: { victory: true, playerDied: false, totalMs: 2000 }
	},

	// ── Deterministic damage reflection (#1330) ──────────────────────────────────
	// Reflection returns the defender's DamageReflection share of a direct hit's net damage to the attacker,
	// bypassing the attacker's mitigation. Authored-only, deterministic (no draw), scoped to direct hits.
	// Mirrors the backend `reflectionKillsAttacker` / `reflectionIgnoresDot` / `drawOrderDodgeOnlyAlignsCrit`.

	// Reflection as a kill condition: a pure tank (no skills, no Toughness — MaxHealth 550 from Strength)
	// returns 100% of every 25 it takes, so the 100-HP enemy dies to its own reflected damage on its 4th attack
	// (tick 40 → 1600ms) while the 550-HP player (down to 450) easily survives.
	{
		name: 'reflectionKillsAttacker',
		player: () =>
			makeBattler(
				[
					{ id: EAttribute.Strength, amount: 100 },
					{ id: EAttribute.DamageReflection, amount: 1.0 }
				],
				[]
			),
		enemy: () => makeBattler([{ id: EAttribute.Strength, amount: 10 }], [makeSkill(25, 400)]),
		expected: { victory: true, playerDied: false, totalMs: 1600 }
	},

	// Reflection ignores DoT: the player bears a constant 50 PoisonDamagePerSecond (2/tick, self-inflicted) and
	// 100% DamageReflection but no skills, against a 100-HP enemy with no skills. DoT applies through the
	// end-of-tick phase, not a direct hit, so it is never reflected — the enemy takes nothing back and never
	// dies, and the player's own poison grinds its 200 HP (Str 30) down, killing it on tick 100 → 4000ms. (Were
	// DoT reflected, the enemy would die at tick 50 / 2000ms.)
	{
		name: 'reflectionIgnoresDot',
		player: () =>
			makeBattler(
				[
					{ id: EAttribute.Strength, amount: 30 },
					{ id: EAttribute.PoisonDamagePerSecond, amount: 50 },
					{ id: EAttribute.DamageReflection, amount: 1.0 }
				],
				[]
			),
		enemy: () => makeBattler([{ id: EAttribute.Strength, amount: 10 }], []),
		expected: { victory: false, playerDied: true, totalMs: 4000 }
	},

	// Simplified draw order — an enemy attack now draws ONE dodge value (not dodge + block), so the player's
	// fractional crit draws interleave at EVEN stream positions (the enemy's single dodge draw sits between
	// consecutive player crit draws). With a real CriticalChance 0.5 against PARITY_SEED the crit draws at
	// stream indices 0, 2, 4, 6 are crit, no, crit, crit; CriticalDamage base 1.5 + 0.5 = 2.0, so the player
	// deals 24, 12, 24, 24 (no Toughness) for a cumulative 24, 36, 60, 84. The 80-HP enemy (Str 6) dies on the
	// tick-40 fire → 1600ms. Had the enemy still drawn TWICE (the old dodge + block), the crit draws would land
	// at indices 0, 3, 6, 9 — crit, no, crit, no → 24, 12, 24, 12 — pushing the kill to tick 50 (2000ms). So this
	// row pins the one-draw enemy attack. The enemy chips 5/tick (DodgeChance 0), leaving the 100-HP player at 85.
	{
		name: 'drawOrderDodgeOnlyAlignsCrit',
		player: () =>
			makeBattler(
				[
					{ id: EAttribute.Strength, amount: 10 },
					{ id: EAttribute.CriticalChance, amount: 0.5 },
					{ id: EAttribute.CriticalDamage, amount: 0.5 }
				],
				[makeSkill(12, 400)]
			),
		enemy: () => makeBattler([{ id: EAttribute.Strength, amount: 6 }], [makeSkill(5, 400)]),
		expected: { victory: true, playerDied: false, totalMs: 1600 }
	},

	// Fractional crit chance against a fixed seed (#941): unlike the forced-1/0 crit rows, CriticalChance is a
	// real 0.5 fraction, so whether each player fire crits depends on the actual Mulberry32 [0,1) draw. With
	// PARITY_SEED the first six crit draws are crit,crit,no,no,crit,crit. Mirrors the backend `fractionalCritChance`.
	//   Player: skill baseDamage 12, cooldown 400 (one crit draw per fire); CriticalDamage base 1.5 + 0.5 = 2.0 →
	//     a crit deals 12×2 = 24, a non-crit 12 (no Toughness). Enemy: Str 10 → MaxHealth 100, no Toughness, no skills.
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
	},

	// ── Item-granted skills (append + order + dedupe) ────────────────────────────
	// The player's battler fields its selected skills plus the skills its equipped items grant (the frontend
	// gathers them via InventoryManager.grantedSkillIds and Battler appends them, de-duplicated). Mirrors the
	// backend `itemGrantsSkill` / `twoItemsGrantSameSkill` / `grantedSkillDuplicatesSelected` /
	// `grantedSkillWithEffects` scenarios.

	// An item grants a skill in addition to a selected one: both fire. Selected (20−2 = 18/hit) and granted
	// (30−2 = 28/hit) on cooldown 400 fire together (ticks 10,20,30) for 46/tick; the 100-HP enemy dies on the
	// tick-30 volley → 1200ms. (The selected skill alone — 18/tick — would win only at 2400ms.)
	{
		name: 'itemGrantsSkill',
		player: () => {
			const selected = granted.register(makeSkill(20, 400));
			const grant = granted.register(makeSkill(30, 400));
			return granted.build([{ id: EAttribute.Strength, amount: 10 }], [selected], [grant]);
		},
		enemy: () => makeBattler([{ id: EAttribute.Strength, amount: 10 }], []),
		expected: { victory: true, playerDied: false, totalMs: 800 }
	},

	// Two equipped items grant the SAME skill: it is de-duplicated to one (not fielded twice). The single
	// granted skill deals 28/hit (cooldown 400); the 100-HP enemy dies on hit 4 at tick 40 → 1600ms. (Two
	// un-deduped copies — 56/tick — would kill on hit 2 at 800ms.)
	{
		name: 'twoItemsGrantSameSkill',
		player: () => {
			const grant = granted.register(makeSkill(30, 400));
			return granted.build([{ id: EAttribute.Strength, amount: 10 }], [], [grant, grant]);
		},
		enemy: () => makeBattler([{ id: EAttribute.Strength, amount: 10 }], []),
		expected: { victory: true, playerDied: false, totalMs: 1600 }
	},

	// A granted skill duplicating a SELECTED skill is de-duplicated (first/selected wins): the player fields it
	// once (22−2 = 20/hit, cooldown 400). The 100-HP enemy dies on hit 5 at tick 50 → 2000ms. (Without dedupe
	// the two copies — 40/tick — would kill on hit 3 at 1200ms.)
	{
		name: 'grantedSkillDuplicatesSelected',
		player: () => {
			const skill = granted.register(makeSkill(22, 400));
			return granted.build([{ id: EAttribute.Strength, amount: 10 }], [skill], [skill]);
		},
		enemy: () => makeBattler([{ id: EAttribute.Strength, amount: 10 }], []),
		expected: { victory: true, playerDied: false, totalMs: 2000 }
	},

	// The SAME skill selected twice is de-duplicated WITHIN the selected loadout (first wins): the player
	// fields it once (22−2 = 20/hit, cooldown 400). The 100-HP enemy dies on hit 5 at tick 50 → 2000ms.
	// (Without intra-selected dedupe the two copies — 40/tick — would kill on hit 3 at 1200ms.) Pins that the
	// frontend mirrors the backend's single Distinct() over the full loadout, not just granted-vs-selected.
	{
		name: 'duplicateSelectedSkill',
		player: () => {
			const skill = granted.register(makeSkill(22, 400));
			return granted.build([{ id: EAttribute.Strength, amount: 10 }], [skill, skill], []);
		},
		enemy: () => makeBattler([{ id: EAttribute.Strength, amount: 10 }], []),
		expected: { victory: true, playerDied: false, totalMs: 2000 }
	},

	// A granted skill carrying an EFFECT works exactly as a selected one would: the item grants a skill whose
	// self +10 Strength buff stacks each fire, ramping its own damage. Str×1.0 raw (no Toughness) deals
	// 8,18,28,38,48,58 (Str climbing 10→60); cumulative 8,26,54,92,140,198 drops the 150-HP enemy on fire 6 at
	// tick 60 → 2400ms — proving granted skills wrap into a full battle skill (charge + effects).
	{
		name: 'grantedSkillWithEffects',
		player: () => {
			const grant = granted.register(
				makeSkill(
					0,
					400,
					[{ attributeId: EAttribute.Strength, multiplier: 1.0 }],
					[makeEffect(110, ESkillEffectTarget.Self, EAttribute.Strength, EModifierType.Additive, 10, PERMANENT)]
				)
			);
			return granted.build([{ id: EAttribute.Strength, amount: 10 }], [], [grant]);
		},
		enemy: () => makeBattler([{ id: EAttribute.Strength, amount: 20 }], []),
		expected: { victory: true, playerDied: false, totalMs: 2000 }
	},

	// ── Direct-hit damage typing: amplification / resistance (#1320 Area B) ──────
	// Skills carry a leaf damage type; the attacker's amplification and defender's resistance for that type's
	// applies() keys multiply the hit as separate budgets — × (1 + amp), × (1 − res), res unclamped — with
	// crit attacker-side (pre-mitigation) and the Toughness curve last. All chances are 0, so outcomes are
	// hand-computable. Mirrors the backend `fireAmplification` … `enemyAmplifiesPlayerResists` scenarios.

	// Attacker amplification: a Fire skill with +0.5 FireAmplification deals 20 × 1.5 = 30/hit (no Toughness), so
	// the 100-HP enemy dies on hit 4 at tick 40 → 1600ms (vs hit 6 / 2400ms un-amplified).
	{
		name: 'fireAmplification',
		player: () =>
			makeBattler(
				[
					{ id: EAttribute.Strength, amount: 10 },
					{ id: EAttribute.FireAmplification, amount: 0.5 }
				],
				[makeSkill(20, 400, [], [], EDamageType.Fire)]
			),
		enemy: () => makeBattler([{ id: EAttribute.Strength, amount: 10 }], []),
		expected: { victory: true, playerDied: false, totalMs: 1600 }
	},

	// Defender resistance: the enemy's +0.5 FireResistance halves a 40-damage Fire hit to 20/hit (no Toughness),
	// so the 100-HP enemy dies on hit 6 at tick 60 → 2400ms (vs hit 3 / 1200ms un-resisted).
	{
		name: 'fireResistance',
		player: () =>
			makeBattler([{ id: EAttribute.Strength, amount: 10 }], [makeSkill(40, 400, [], [], EDamageType.Fire)]),
		enemy: () =>
			makeBattler(
				[
					{ id: EAttribute.Strength, amount: 10 },
					{ id: EAttribute.FireResistance, amount: 0.5 }
				],
				[]
			),
		expected: { victory: true, playerDied: false, totalMs: 2000 }
	},

	// Vulnerability (res < 0): a −1.0 FireResistance doubles the incoming Fire hit (factor 1 − (−1) = 2). A
	// 20-damage hit becomes 40/hit (no Toughness), so the 100-HP enemy dies on hit 3 at tick 30 → 1200ms (vs hit
	// 6 / 2400ms at res 0). Resistance is deliberately left unclamped.
	{
		name: 'fireVulnerability',
		player: () =>
			makeBattler([{ id: EAttribute.Strength, amount: 10 }], [makeSkill(20, 400, [], [], EDamageType.Fire)]),
		enemy: () =>
			makeBattler(
				[
					{ id: EAttribute.Strength, amount: 10 },
					{ id: EAttribute.FireResistance, amount: -1.0 }
				],
				[]
			),
		expected: { victory: true, playerDied: false, totalMs: 1200 }
	},

	// Absorption (res > 1): a +2.0 FireResistance drives the post-resistance hit negative (20 × (1 − 2) = −20),
	// so an absorbed hit deals no damage — the Toughness curve never applies, and the heal is capped at MaxHealth (the
	// enemy is already full, so it stays at 100). The enemy never loses health and never dies; dealing no damage
	// back, the battle runs to the timeout. (At res 0 the player's 18/hit would win by 2400ms.)
	{
		name: 'fireAbsorption',
		player: () => makeBattler([], [makeSkill(20, 400, [], [], EDamageType.Fire)]),
		enemy: () =>
			makeBattler(
				[
					{ id: EAttribute.Strength, amount: 10 },
					{ id: EAttribute.FireResistance, amount: 2.0 }
				],
				[]
			),
		expected: { victory: false, playerDied: false, totalMs: DEFAULT_MAX_BATTLE_MS }
	},

	// Crit multiplies pre-mitigation, so it punches through resistance AND the Toughness curve: a forced crit
	// (CriticalDamage base 1.5 + 0.5 = 2.0) on a Fire hit. The enemy's Toughness 20 vs the level-1 player halves
	// the post-resistance hit, so a normal hit deals 20 × (1 − 0.5 res) × 0.5 = 5 while the crit deals
	// 20 × 2 × 0.5 × 0.5 = 10/hit — dropping the 50-HP enemy on hit 5 at tick 50 → 2000ms.
	{
		name: 'critPunchesThroughTyped',
		player: () =>
			makeBattler(
				[
					{ id: EAttribute.CriticalChance, amount: 1 },
					{ id: EAttribute.CriticalDamage, amount: 0.5 }
				],
				[makeSkill(20, 400, [], [], EDamageType.Fire)]
			),
		enemy: () =>
			makeBattler(
				[
					{ id: EAttribute.Toughness, amount: 20 },
					{ id: EAttribute.FireResistance, amount: 0.5 }
				],
				[]
			),
		expected: { victory: true, playerDied: false, totalMs: 2000 }
	},

	// Reduce-to-today identity: a Fire-typed skill with no amplification or resistance authored anywhere behaves
	// exactly like the untyped/physical hit it used to be — × (1 + 0) and × (1 − 0) are exact 1.0 factors. 20
	// raw → 20/hit (no Toughness), so the 100-HP enemy dies on hit 5 → 2000ms, identical to a baseDamage-20 physical skill.
	{
		name: 'typedFireNoModifiersUnchanged',
		player: () =>
			makeBattler([{ id: EAttribute.Strength, amount: 10 }], [makeSkill(20, 400, [], [], EDamageType.Fire)]),
		enemy: () => makeBattler([{ id: EAttribute.Strength, amount: 10 }], []),
		expected: { victory: true, playerDied: false, totalMs: 2000 }
	},

	// Bidirectional: the ENEMY amplifies and the PLAYER resists. The enemy's +1.0 FireAmplification doubles its
	// 20-damage Fire skill to 40; the player's +0.5 FireResistance halves that to 20/hit (no Toughness). The
	// player (no skills) dies on the enemy's hit 6 at tick 60 → 2400ms. (Without the enemy amp it would be
	// 8/hit → 5200ms; without the player resist, 38/hit → 1200ms.)
	{
		name: 'enemyAmplifiesPlayerResists',
		player: () =>
			makeBattler(
				[
					{ id: EAttribute.Strength, amount: 10 },
					{ id: EAttribute.FireResistance, amount: 0.5 }
				],
				[]
			),
		enemy: () =>
			makeBattler(
				[
					{ id: EAttribute.Strength, amount: 10 },
					{ id: EAttribute.FireAmplification, amount: 1.0 }
				],
				[makeSkill(20, 400, [], [], EDamageType.Fire)]
			),
		expected: { victory: false, playerDied: true, totalMs: 2000 }
	},

	// ── Typed damage-over-time: per-type accumulators + live resistance (#1320 Area C) ─────────
	// DoT is encoded by which per-second accumulator an effect targets; the end-of-tick phase loops the DoT
	// types in fixed order, applying each type's resistance sampled live off the bearer and bypassing mitigation.
	// A bearer is the defender of its own DoT. Mirrors the backend `poisonResistanceSlowsKill` …
	// `poisonVulnerabilityDebuffSampledLive` scenarios.

	// DoT resistance slows the kill: the enemy's +0.5 PoisonResistance halves its constant 50
	// PoisonDamagePerSecond (2/tick → 1/tick), so the 50-HP enemy dies on tick 50 → 2000ms.
	{
		name: 'poisonResistanceSlowsKill',
		player: () => makeBattler([{ id: EAttribute.Strength, amount: 0 }], []),
		enemy: () =>
			makeBattler(
				[
					{ id: EAttribute.Strength, amount: 0 },
					{ id: EAttribute.PoisonDamagePerSecond, amount: 50 },
					{ id: EAttribute.PoisonResistance, amount: 0.5 }
				],
				[]
			),
		expected: { victory: true, playerDied: false, totalMs: 2000 }
	},

	// DoT vulnerability (res < 0): a −1.0 PoisonResistance doubles the constant 50 PoisonDamagePerSecond
	// (2/tick → 4/tick), so the 50-HP enemy dies on tick 13 → 520ms. Resistance is left unclamped.
	{
		name: 'poisonVulnerabilityHastensKill',
		player: () => makeBattler([{ id: EAttribute.Strength, amount: 0 }], []),
		enemy: () =>
			makeBattler(
				[
					{ id: EAttribute.Strength, amount: 0 },
					{ id: EAttribute.PoisonDamagePerSecond, amount: 50 },
					{ id: EAttribute.PoisonResistance, amount: -1.0 }
				],
				[]
			),
		expected: { victory: true, playerDied: false, totalMs: 520 }
	},

	// DoT absorption (res > 1): a +2.0 PoisonResistance drives the tick negative (2 × (1 − 2) = −2), so the
	// enemy's own poison heals it — it never dies and the battle runs to the timeout.
	{
		name: 'poisonAbsorptionPreventsDeath',
		player: () => makeBattler([{ id: EAttribute.Strength, amount: 0 }], []),
		enemy: () =>
			makeBattler(
				[
					{ id: EAttribute.Strength, amount: 0 },
					{ id: EAttribute.PoisonDamagePerSecond, amount: 50 },
					{ id: EAttribute.PoisonResistance, amount: 2.0 }
				],
				[]
			),
		expected: { victory: false, playerDied: false, totalMs: DEFAULT_MAX_BATTLE_MS }
	},

	// Multi-type DoT sums every accumulator in fixed order: Bleed 50 (2/tick) + Poison 100 (4/tick) + Burn
	// 25 (1/tick) = 7/tick, no resistance. The 350-HP enemy (Str 60) dies on tick 50 → 2000ms.
	{
		name: 'multiTypeDotSum',
		player: () => makeBattler([{ id: EAttribute.Strength, amount: 0 }], []),
		enemy: () =>
			makeBattler(
				[
					{ id: EAttribute.Strength, amount: 60 },
					{ id: EAttribute.BleedDamagePerSecond, amount: 50 },
					{ id: EAttribute.PoisonDamagePerSecond, amount: 100 },
					{ id: EAttribute.BurnDamagePerSecond, amount: 25 }
				],
				[]
			),
		expected: { victory: true, playerDied: false, totalMs: 2000 }
	},

	// Reduce-to-today identity: a single typed DoT with no resistance behaves exactly like the former single
	// accumulator. A constant 50 BurnDamagePerSecond → 2/tick kills the 50-HP enemy on tick 25 → 1000ms.
	{
		name: 'typedBurnNoResistanceUnchanged',
		player: () => makeBattler([{ id: EAttribute.Strength, amount: 0 }], []),
		enemy: () =>
			makeBattler(
				[
					{ id: EAttribute.Strength, amount: 0 },
					{ id: EAttribute.BurnDamagePerSecond, amount: 50 }
				],
				[]
			),
		expected: { victory: true, playerDied: false, totalMs: 1000 }
	},

	// Cross-cutting resistance: burn resists as burn + fire + elemental + dot, so the enemy's +0.5
	// FireResistance halves its constant 250 BurnDamagePerSecond (10/tick → 5/tick) — dies on tick 10 → 400ms.
	{
		name: 'burnResistedByFireResistance',
		player: () => makeBattler([{ id: EAttribute.Strength, amount: 0 }], []),
		enemy: () =>
			makeBattler(
				[
					{ id: EAttribute.Strength, amount: 0 },
					{ id: EAttribute.BurnDamagePerSecond, amount: 250 },
					{ id: EAttribute.FireResistance, amount: 0.5 }
				],
				[]
			),
		expected: { victory: true, playerDied: false, totalMs: 400 }
	},

	// Caster amplification is FROZEN into the accumulator at apply time: the player's +1.0 PoisonAmplification
	// doubles the poison its skill applies (base 25 → 50 PoisonDamagePerSecond → 2/tick). Fired once at tick
	// 50 (cooldown 2000), the 50-HP enemy then loses 2/tick and dies on tick 74 → 2960ms.
	{
		name: 'poisonCasterAmplificationFreezes',
		player: () =>
			makeBattler(
				[{ id: EAttribute.PoisonAmplification, amount: 1.0 }],
				[
					makeSkill(
						0,
						2000,
						[],
						[
							makeEffect(
								212,
								ESkillEffectTarget.Opponent,
								EAttribute.PoisonDamagePerSecond,
								EModifierType.Additive,
								25,
								PERMANENT
							)
						]
					)
				]
			),
		enemy: () => makeBattler([{ id: EAttribute.Strength, amount: 0 }], []),
		expected: { victory: true, playerDied: false, totalMs: 2960 }
	},

	// Resistance is sampled LIVE, not frozen: the enemy carries a constant 50 PoisonDamagePerSecond (2/tick)
	// and the player applies a permanent −4.0 PoisonResistance vulnerability debuff once at tick 5 (cooldown
	// 200). The first four ticks deal 2 (50→42); from tick 5 the live-sampled resistance (factor 5) makes the
	// same poison deal 10/tick, killing the enemy on tick 9 → 360ms — before the skill's tick-10 re-fire.
	{
		name: 'poisonVulnerabilityDebuffSampledLive',
		player: () =>
			makeBattler(
				[{ id: EAttribute.Strength, amount: 0 }],
				[
					makeSkill(
						0,
						200,
						[],
						[
							makeEffect(
								213,
								ESkillEffectTarget.Opponent,
								EAttribute.PoisonResistance,
								EModifierType.Additive,
								-4.0,
								PERMANENT
							)
						]
					)
				]
			),
		enemy: () =>
			makeBattler(
				[
					{ id: EAttribute.Strength, amount: 0 },
					{ id: EAttribute.PoisonDamagePerSecond, amount: 50 }
				],
				[]
			),
		expected: { victory: true, playerDied: false, totalMs: 360 }
	},

	// ── Multi-typed direct hits: weighted portion split (#1343 / #1385) ──────────
	// A skill's raw is split across weighted portions (raw × weight ÷ Σweights), each run through the single-type
	// pipeline under its own type and the nets summed. A single crit multiplies every portion; a single dodge
	// zeroes the whole hit. Mirrors the backend `multiTypeSplitResistsOneType` … `singlePortionNonUnitWeightIdentity`.

	// 60/40 split with resistance on only one type. A raw-100 hit splits into 60 Physical + 40 Fire; the enemy
	// resists Fire 0.5 (Physical unresisted), no Toughness: 60 + 40×0.5 = 80/hit. The 250-HP enemy (Str 40) dies on
	// hit 4 at tick 40 → 1600ms. (Un-resisted 100/hit would kill on hit 3 / 1200ms, so the per-portion resist matters.)
	{
		name: 'multiTypeSplitResistsOneType',
		player: () =>
			makeBattler(
				[{ id: EAttribute.Strength, amount: 10 }],
				[
					makeMultiTypeSkill(100, 400, [
						{ type: EDamageType.Physical, weight: 60 },
						{ type: EDamageType.Fire, weight: 40 }
					])
				]
			),
		enemy: () =>
			makeBattler(
				[
					{ id: EAttribute.Strength, amount: 40 },
					{ id: EAttribute.FireResistance, amount: 0.5 }
				],
				[]
			),
		expected: { victory: true, playerDied: false, totalMs: 1600 }
	},

	// A single crit multiplies EVERY portion. Forced crit (CriticalDamage 1.5 + 0.5 = 2) on a [Physical 50, Fire 50]
	// split of raw 20 → 10 each, ×2 → 20 each = 40/hit (no Toughness). The 100-HP enemy dies on hit 3 at tick 30 →
	// 1200ms. A crit on one portion only (30/hit) would kill on hit 4 (1600ms); no crit (20/hit) on hit 5 (2000ms).
	{
		name: 'multiTypeCritAppliesToAllPortions',
		player: () =>
			makeBattler(
				[
					{ id: EAttribute.CriticalChance, amount: 1 },
					{ id: EAttribute.CriticalDamage, amount: 0.5 }
				],
				[
					makeMultiTypeSkill(20, 400, [
						{ type: EDamageType.Physical, weight: 50 },
						{ type: EDamageType.Fire, weight: 50 }
					])
				]
			),
		enemy: () => makeBattler([{ id: EAttribute.Strength, amount: 10 }], []),
		expected: { victory: true, playerDied: false, totalMs: 1200 }
	},

	// A single dodge zeroes the WHOLE multi-typed hit. The enemy's [Physical 500, Fire 500] split of a 1000-damage
	// hit would one-shot the 100-HP player, but DodgeChance 1 negates every hit, so the player survives and grinds
	// the enemy down (20/hit, no Toughness) for the win on hit 5 at tick 50 → 2000ms. (Zeroing only one portion
	// would leave 500 through and kill the player at tick 10.)
	{
		name: 'multiTypeDodgeZeroesWholeHit',
		player: () =>
			makeBattler(
				[
					{ id: EAttribute.Strength, amount: 10 },
					{ id: EAttribute.DodgeChance, amount: 1 }
				],
				[makeSkill(20, 400)]
			),
		enemy: () =>
			makeBattler(
				[{ id: EAttribute.Strength, amount: 10 }],
				[
					makeMultiTypeSkill(1000, 400, [
						{ type: EDamageType.Physical, weight: 500 },
						{ type: EDamageType.Fire, weight: 500 }
					])
				]
			),
		expected: { victory: true, playerDied: false, totalMs: 2000 }
	},

	// Per-portion vulnerability (res < 0). A [Physical 50, Fire 50] split of raw 40 → 20 each; the enemy's −1.0
	// FireResistance doubles only the Fire portion (20 → 40), Physical stays 20: 60/hit. The 180-HP enemy (Str 26)
	// dies on hit 3 at tick 30 → 1200ms. (No vulnerability — 40/hit — would kill on hit 5 / 2000ms.)
	{
		name: 'multiTypePerPortionVulnerability',
		player: () =>
			makeBattler(
				[{ id: EAttribute.Strength, amount: 10 }],
				[
					makeMultiTypeSkill(40, 400, [
						{ type: EDamageType.Physical, weight: 50 },
						{ type: EDamageType.Fire, weight: 50 }
					])
				]
			),
		enemy: () =>
			makeBattler(
				[
					{ id: EAttribute.Strength, amount: 26 },
					{ id: EAttribute.FireResistance, amount: -1.0 }
				],
				[]
			),
		expected: { victory: true, playerDied: false, totalMs: 1200 }
	},

	// Per-portion absorption with the order-dependent heal cap. A [Physical 20, Fire 80] split of raw 100 against an
	// enemy absorbing Fire (FireResistance 2.0) at full health: the Physical portion deals 20 (100 → 80, opening 20
	// room), then the Fire portion's −80 absorption heal is CAPPED at that 20 room (back to 100). Net 20 + (−20) =
	// 0/hit, so the enemy never dies — a timeout draw. The fixed Physical-first order is what lets the heal land.
	{
		name: 'multiTypePerPortionAbsorptionCap',
		player: () =>
			makeBattler(
				[],
				[
					makeMultiTypeSkill(100, 400, [
						{ type: EDamageType.Physical, weight: 20 },
						{ type: EDamageType.Fire, weight: 80 }
					])
				]
			),
		enemy: () =>
			makeBattler(
				[
					{ id: EAttribute.Strength, amount: 10 },
					{ id: EAttribute.FireResistance, amount: 2.0 }
				],
				[]
			),
		expected: { victory: false, playerDied: false, totalMs: DEFAULT_MAX_BATTLE_MS }
	},

	// Reduce-to-single-portion identity for a non-unit weight: raw × w ÷ w = raw exactly. A lone Fire portion at
	// weight 2 with no amp/resist deals 20/hit, so the 100-HP enemy dies on hit 5 → 2000ms — identical to
	// typedFireNoModifiersUnchanged, proving a single portion (any weight) is the single-type hit.
	{
		name: 'singlePortionNonUnitWeightIdentity',
		player: () =>
			makeBattler(
				[{ id: EAttribute.Strength, amount: 10 }],
				[makeMultiTypeSkill(20, 400, [{ type: EDamageType.Fire, weight: 2 }])]
			),
		enemy: () => makeBattler([{ id: EAttribute.Strength, amount: 10 }], []),
		expected: { victory: true, playerDied: false, totalMs: 2000 }
	},

	// ── Weapon-match gate + virtual-fists punch (#1342) ──────────────────────────
	// Mirrors the backend `weaponGateDimsOffWeaponSelected` / `bareHandsPunchFieldsAlongsideAgnostic`. The
	// fielded set is filtered by the equipped weapon at assembly (setup-time, never per tick), identically
	// FE/BE. The weapon's signature / the bare-hands punch ride grantedSkillIds as InventoryManager appends
	// them; equippedWeaponType drives the gate.

	// A Sword is wielded: the selected Axe skill is off-weapon (dormant) and never fires; only the sword's
	// own-type signature (28/hit, cooldown 400, no Toughness) does, dropping the 100-HP enemy on hit 4 at
	// tick 40 → 1600ms. (Un-dimmed, the Axe + signature pair — 56/tick — would kill on hit 2 at 800ms.)
	{
		name: 'weaponGateDimsOffWeaponSelected',
		player: () => {
			const axe = granted.register(makeSkill(28, 400, [], [], EDamageType.Axe));
			const swordSignature = granted.register(makeSkill(28, 400, [], [], EDamageType.Sword));
			return granted.build([{ id: EAttribute.Strength, amount: 0 }], [axe], [swordSignature], EDamageType.Sword);
		},
		enemy: () => makeBattler([{ id: EAttribute.Strength, amount: 10 }], []),
		expected: { victory: true, playerDied: false, totalMs: 1600 }
	},

	// Bare-handed: the virtual-fists punch is fielded alongside an agnostic selected skill. Selected Physical
	// 16/hit + punch (Unarmed) 24/hit fire together (cooldown 400) for 40/tick; the 100-HP enemy dies on the
	// tick-30 volley → 1200ms. (The agnostic skill alone — 16/tick — would win only on hit 7 at 2800ms.)
	{
		name: 'bareHandsPunchFieldsAlongsideAgnostic',
		player: () => {
			const physical = granted.register(makeSkill(16, 400, [], [], EDamageType.Physical));
			const punch = granted.register(makeSkill(24, 400, [], [], EDamageType.Unarmed));
			return granted.build([{ id: EAttribute.Strength, amount: 0 }], [physical], [punch], EDamageType.Unarmed);
		},
		enemy: () => makeBattler([{ id: EAttribute.Strength, amount: 10 }], []),
		expected: { victory: true, playerDied: false, totalMs: 1200 }
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
		expect(player.attributes.getValue(EAttribute.Toughness)).toBe(60); // 2·Endurance(30)
		expect(player.attributes.getValue(EAttribute.CooldownRecovery)).toBeCloseTo(1.09, 10);
		expect(player.cdMultiplier).toBeCloseTo(1.09, 10);

		expect(enemy.attributes.getValue(EAttribute.MaxHealth)).toBe(400);
		expect(enemy.attributes.getValue(EAttribute.Toughness)).toBe(30); // 2·Endurance(15)
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
		expect(player.attributes.getValue(EAttribute.Toughness)).toBe(40); // 2·Endurance(20)
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
			{ id: EAttribute.Toughness, amount: 60 }, // 2*30
			{ id: EAttribute.CooldownRecovery, amount: 1.09 } // 1 + 0.004*20 + 0.001*10
		];
		const player = makeBattler(playerFinalAttrs, [
			makeSkill(10, 1200, [{ attributeId: EAttribute.Strength, multiplier: 1.5 }])
		]);
		const enemy = scenarios[0].enemy();

		// Derived stats are now DOUBLED.
		expect(player.attributes.getValue(EAttribute.MaxHealth)).toBe(1800);
		expect(player.attributes.getValue(EAttribute.Toughness)).toBe(120); // injected 60 + derived 2·30
		expect(player.attributes.getValue(EAttribute.CooldownRecovery)).toBeCloseTo(2.18, 10);
		expect(player.cdMultiplier).toBeCloseTo(2.18, 10);

		const result = new BattleSimulator(player, enemy, PARITY_SEED).simulate();
		expect(result.totalMs).toBe(6720);
		expect(result.totalMs).toBeLessThan(13440);
	});
});
