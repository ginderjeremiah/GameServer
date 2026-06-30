using Game.Core.Attributes;
using Game.Core.Attributes.Modifiers;
using Game.Core.Battle;
using Game.Core.Enemies;
using Game.Core.Items;
using Game.Core.Players;
using Game.Core.Players.Inventories;
using Game.Core.Skills;
using Game.Core.TestInfrastructure.Builders;
using Xunit;

namespace Game.Core.Tests.Battle
{
    /// <summary>
    /// Cross-implementation parity matrix for the deterministic battle simulation.
    /// Every scenario here MUST be mirrored — with identical inputs and identical
    /// expected <c>totalMs</c>/outcome — in the frontend suite
    /// <c>UI/src/tests/lib/battle/battle-simulation-parity.test.ts</c>.
    /// The two simulators run on both the client and the server (the anti-cheat
    /// replay depends on them agreeing), so any new battle behaviour added on one
    /// side should gain a matching row here on the other.
    /// </summary>
    public class BattleSimulatorParityTests
    {
        /// <summary>
        /// A single deterministic battle scenario: the inputs that build both
        /// combatants plus the exact result the simulation must produce.
        /// </summary>
        public sealed record ParityScenario(
            Func<Battler> Player,
            Func<Battler> Enemy,
            bool ExpectedVictory,
            bool ExpectedPlayerDied,
            int ExpectedTotalMs,
            int? MaxMs = null);

        /// <summary>
        /// The shared scenario table. Keyed by name so xUnit can drive a
        /// <see cref="TheoryAttribute"/> over the names without serializing the
        /// (non-serializable) builders; the test resolves the full scenario by name.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, ParityScenario> Scenarios =
            new Dictionary<string, ParityScenario>
            {
                // Single skill, CooldownRecovery > 0 — exercises the cdMultiplier path.
                //   Player: MaxHealth=900, Toughness=60 (2·End30), CDR=1.09 → cdMult=1.09
                //     charge/tick = 40*1.09 = 43.6, fires every 28 ticks (28*43.6=1220.8≥1200)
                //     damage = 10 + 50*1.5 = 85; enemy Toughness 30 vs the level-1 player → 85×20/(30+20) = 34/hit
                //   Enemy:  MaxHealth=400, Toughness=30; damage = 5 × 20/(60+20) = 1.25/hit (player survives)
                //   12 hits to kill (12*34=408>400), at ticks 28..336 → 13440ms
                ["cooldownRecovery"] = new ParityScenario(
                    Player: () => MakeBattler(
                        strength: 50, endurance: 30, agility: 20, dexterity: 10,
                        skills: [MakeSkill(1, baseDamage: 10, cooldownMs: 1200, mult: EAttribute.Strength, multAmount: 1.5)]),
                    Enemy: () => MakeEnemy(
                        strength: 10, endurance: 15,
                        skills: [MakeSkill(2, baseDamage: 5, cooldownMs: 2000)]),
                    ExpectedVictory: true,
                    ExpectedPlayerDied: false,
                    ExpectedTotalMs: 13440),

                // Two player skills firing on different cooldowns against an enemy that takes real damage — a
                // genuine multi-skill exchange. Both defenders have Toughness now.
                //   Player: MaxHealth=600, Toughness=40 (2·End20), cdMult=1
                //     skillA: 20 + 30*1.0 = 50 raw → enemy Toughness 30 → 50×20/50 = 20/hit, every 20 ticks
                //     skillB: 25 raw → 25×20/50 = 10/hit, every 30 ticks
                //   Enemy:  MaxHealth=450, Toughness=30; attack 30 raw → player Toughness 40 → 30×20/60 = 10/hit, every 25 ticks
                //   Cumulative player damage first reaches 450 at tick 340 → 13600ms (player survives the chip).
                ["multiSkill"] = new ParityScenario(
                    Player: () => MakeBattler(
                        strength: 30, endurance: 20,
                        skills:
                        [
                            MakeSkill(1, baseDamage: 20, cooldownMs: 800, mult: EAttribute.Strength, multAmount: 1.0),
                            MakeSkill(2, baseDamage: 25, cooldownMs: 1200),
                        ]),
                    Enemy: () => MakeEnemy(
                        strength: 20, endurance: 15,
                        skills: [MakeSkill(3, baseDamage: 30, cooldownMs: 1000)]),
                    ExpectedVictory: true,
                    ExpectedPlayerDied: false,
                    ExpectedTotalMs: 13600),

                // Both combatants have so much Toughness (Endurance 50 → Toughness 100) that each 5-damage hit is
                // reduced to a trickle (5 × 20/(100+20) ≈ 0.83), and with their Endurance-inflated MaxHealth
                // (1050 each) that trickle cannot kill within the cap — so the battle runs to the timeout as a
                // draw. The curve never clamps a hit fully to 0 (it asymptotes below 100%), so the stalemate now
                // comes from EHP outpacing the trickle rather than a hard floor.
                ["highToughnessTrickle"] = new ParityScenario(
                    Player: () => MakeBattler(
                        strength: 0, endurance: 50,
                        skills: [MakeSkill(1, baseDamage: 5, cooldownMs: 1000)]),
                    Enemy: () => MakeEnemy(
                        strength: 0, endurance: 50,
                        skills: [MakeSkill(2, baseDamage: 5, cooldownMs: 1000)]),
                    ExpectedVictory: false,
                    ExpectedPlayerDied: false,
                    ExpectedTotalMs: GameConstants.DefaultMaxBattleMs),

                // Neither side has any skills, so no damage is ever dealt and the
                // battle runs to the default timeout.
                ["noSkills"] = new ParityScenario(
                    Player: () => MakeBattler(strength: 10, endurance: 10, skills: []),
                    Enemy: () => MakeEnemy(strength: 10, endurance: 10, skills: []),
                    ExpectedVictory: false,
                    ExpectedPlayerDied: false,
                    ExpectedTotalMs: GameConstants.DefaultMaxBattleMs),

                // A dedicated-boss encounter: a higher-level boss bringing its FULL authored loadout
                // (3 skills, more than the random 4-skill cap would force a narrowing on) against a player
                // who out-tanks it. Exercises the multi-skill enemy path that the boss challenge introduces.
                // Under the curve the boss's smaller skills no longer clamp to 0 — they all chip — so the player
                // is built tankier (more Endurance) to still out-attrition it.
                //   Player: Str=60, End=50 → MaxHealth=1350, Toughness=100, cdMult=1.
                //     skill: 10 + 60*2.0 = 130 raw → boss Toughness 60 → 130×20/80 = 32.5/hit, every 25 ticks.
                //   Boss:   Str=20, End=30 → MaxHealth=750, Toughness=60, cdMult=1.
                //     3 skills (50/20/30 raw) → player Toughness 100 → 8.33 + 3.33 + 5 = 16.67/volley every 25 ticks.
                //   24 player hits reach 780 ≥ 750 at tick 600 → 24000ms; the boss's ~16.67/volley never threatens
                //   the 1350-HP player.
                ["bossFullLoadout"] = new ParityScenario(
                    Player: () => MakeBattler(
                        strength: 60, endurance: 50,
                        skills: [MakeSkill(1, baseDamage: 10, cooldownMs: 1000, mult: EAttribute.Strength, multAmount: 2.0)]),
                    Enemy: () => MakeEnemy(
                        strength: 20, endurance: 30,
                        skills:
                        [
                            MakeSkill(2, baseDamage: 50, cooldownMs: 1000),
                            MakeSkill(3, baseDamage: 20, cooldownMs: 1000),
                            MakeSkill(4, baseDamage: 30, cooldownMs: 1000),
                        ]),
                    ExpectedVictory: true,
                    ExpectedPlayerDied: false,
                    ExpectedTotalMs: 24000),

                // The cooldownRecovery matchup capped well before either skill fires:
                // the simulation stops at maxMs with no winner.
                ["maxMsCap"] = new ParityScenario(
                    Player: () => MakeBattler(
                        strength: 50, endurance: 30, agility: 20, dexterity: 10,
                        skills: [MakeSkill(1, baseDamage: 10, cooldownMs: 1200, mult: EAttribute.Strength, multAmount: 1.5)]),
                    Enemy: () => MakeEnemy(
                        strength: 10, endurance: 15,
                        skills: [MakeSkill(2, baseDamage: 5, cooldownMs: 2000)]),
                    ExpectedVictory: false,
                    ExpectedPlayerDied: false,
                    ExpectedTotalMs: 200,
                    MaxMs: 200),

                // The only scenario whose player is built through the equipped-item + applied-mod
                // attribute-composition path (the rest build the player from raw stat allocations).
                // An item and two mods (prefix + suffix) each contribute Strength, so dropping any
                // one source changes the kill count; the item's Agility and the prefix's Dexterity
                // additionally feed CooldownRecovery, so the whole merge is exercised end to end.
                //   Allocations: Str=20, End=20.  Item: +10 Str, +20 Agi.  Prefix: +8 Str, +20 Dex.  Suffix: +7 Str.
                //   Merged: Str=45, End=20, Agi=20, Dex=20 → MaxHealth=675, Toughness=40, CDR=1.10 → cdMult=1.10.
                //   Player skill: 10 + 45*1.5 = 77.5 raw → enemy Toughness 30 → 77.5×20/50 = 31/hit, fires every
                //     28 ticks (charge/tick = 40*1.10 = 44, 44*28=1232 ≥ 1200).
                //   Enemy: MaxHealth=400, Toughness=30; attack 5 → player Toughness 40 → 5×20/60 = 1.67/hit (the player never dies).
                //   13 hits reach 403 ≥ 400 at tick 364 → 14560ms (proof the merged attributes drive the outcome).
                ["equippedItemWithMods"] = new ParityScenario(
                    Player: () => MakeBattlerWithEquipment(
                        strength: 20, endurance: 20,
                        skills: [MakeSkill(1, baseDamage: 10, cooldownMs: 1200, mult: EAttribute.Strength, multAmount: 1.5)],
                        itemAttributes: [ItemAttr(EAttribute.Strength, 10), ItemAttr(EAttribute.Agility, 20)],
                        mods:
                        [
                            MakeMod(10, EItemModType.Prefix, [ItemAttr(EAttribute.Strength, 8), ItemAttr(EAttribute.Dexterity, 20)]),
                            MakeMod(11, EItemModType.Suffix, [ItemAttr(EAttribute.Strength, 7)]),
                        ]),
                    Enemy: () => MakeEnemy(
                        strength: 10, endurance: 15,
                        skills: [MakeSkill(2, baseDamage: 5, cooldownMs: 2000)]),
                    ExpectedVictory: true,
                    ExpectedPlayerDied: false,
                    ExpectedTotalMs: 14560),

                // Fractional enemy attribute distribution (#941): the only scenario whose enemy attribute is
                // built from a FRACTIONAL per-level AttributeDistribution (BaseAmount 0 + 2.5/level at level 3
                // → Strength 7.5) rather than an integer BaseAmount at level 1 — the production path
                // EnemyInstance.FromSource serializes the resulting double to the client, which re-derives it.
                // Strength feeds only MaxHealth here: 50 + 5×7.5 = 87.5, a fractional max with a TIGHT kill margin.
                //   Player: skill baseDamage 29, no multiplier, cooldown 400 → 29/hit (the enemy has no Toughness)
                //     every 10 ticks.
                //   Enemy:  MaxHealth 87.5, no Toughness, no skills (the player never dies).
                //   Cumulative 29,58,87 (the 87.5 survives the 87), 116 → dies on hit 4 at tick 40 → 1600ms. Had
                //   the .5 been lost to a float roundtrip (MaxHealth 87) the enemy would die a hit earlier at 1200.
                ["fractionalEnemyDistribution"] = new ParityScenario(
                    Player: () => MakeBattler(
                        strength: 0, endurance: 0,
                        skills: [MakeSkill(1, baseDamage: 29, cooldownMs: 400)]),
                    Enemy: () => MakeEnemy(
                        strength: 0, endurance: 0,
                        skills: [],
                        level: 3,
                        perLevelDistributions: [(EAttribute.Strength, 0m, 2.5m)]),
                    ExpectedVictory: true,
                    ExpectedPlayerDied: false,
                    ExpectedTotalMs: 1600),

                // A self Strength buff: the hit that CARRIES the effect uses the pre-effect attributes, and
                // only subsequent hits see the boost (which also raises damage via the Strength→damage path)
                // — and re-applying it every fire now STACKS (each fire adds another +10), so the buff
                // compounds rather than refreshing.
                //   Player: Str=10, cdMult=1; skill = Str×1.0 raw, fires every 10 ticks (cooldown 400).
                //     Effect: Self +10 Strength (additive), effectively permanent — a new +10 stack per fire.
                //   Enemy:  Str=10 → MaxHealth=100, no Toughness, no skills.
                //   Fire 1 deals Str(10) then stacks → Str 20; fire 2 deals 20 → Str 30; fire 3 deals 30 → Str 40;
                //   fire 4 deals 40.
                //   Enemy HP 100 → 90,70,40,0: dies on fire 4 at tick 40 → 1600ms.
                //   (Refresh-only stacking — the old rule — would cap Str at 20 and kill later.)
                ["selfStrengthBuffStacksEachFire"] = new ParityScenario(
                    Player: () => MakeBattler(
                        strength: 10, endurance: 0,
                        skills:
                        [
                            MakeSkill(1, baseDamage: 0, cooldownMs: 400, mult: EAttribute.Strength, multAmount: 1.0,
                                effects: [MakeEffect(100, ESkillEffectTarget.Self, EAttribute.Strength, EModifierType.Additive, 10, Permanent)]),
                        ]),
                    Enemy: () => MakeEnemy(
                        strength: 10, endurance: 0,
                        skills: []),
                    ExpectedVictory: true,
                    ExpectedPlayerDied: false,
                    ExpectedTotalMs: 1600),

                // A self CooldownRecovery buff is read live each tick AND stacks on each fire, so the fire
                // interval keeps shortening as the buff compounds — proving both the live CDR read and stacking.
                //   Player: Str=20, base CDR=1 (cdMult=1); skill = Str×1.0 raw, cooldown 400. Each hit deals 20
                //     (the enemy has no Toughness).
                //     Effect: Self +1.0 CooldownRecovery (additive), effectively permanent — a stack per fire.
                //   Enemy:  Str=10 → MaxHealth=100, no Toughness, no skills.
                //   Fire 1 at tick 10 (cdMult=1) applies a stack → cdMult=2; charge/tick then 80 → next fire 5
                //   ticks later (15, cdMult=3), then 4 (19, cdMult=4), 3 (22, 5), 2 (24). Five 20-dmg hits drop
                //   the 100-HP enemy on fire 5 at tick 24 → 960ms. (Refresh-only held cdMult=2 → slower.)
                ["cdrBuffShortensFireInterval"] = new ParityScenario(
                    Player: () => MakeBattler(
                        strength: 20, endurance: 0,
                        skills:
                        [
                            MakeSkill(1, baseDamage: 0, cooldownMs: 400, mult: EAttribute.Strength, multAmount: 1.0,
                                effects: [MakeEffect(101, ESkillEffectTarget.Self, EAttribute.CooldownRecovery, EModifierType.Additive, 1.0, Permanent)]),
                        ]),
                    Enemy: () => MakeEnemy(
                        strength: 10, endurance: 0,
                        skills: []),
                    ExpectedVictory: true,
                    ExpectedPlayerDied: false,
                    ExpectedTotalMs: 960),

                // Same-tick ordering: an earlier loadout slot's self buff influences a later slot firing on the
                // same tick. Slot 0 buffs Strength after dealing its (pre-buff) damage, then slot 1 — firing
                // after it on the same tick — deals its damage from the already-buffed Strength. The buff also
                // STACKS on each fire, so the per-volley damage ramps.
                //   Player: Str=10; slot0 = Str×1.0 (Self +10 Str, permanent — a stack per fire), slot1 = Str×1.0
                //     (no effect), both cooldown 400 so they fire together at ticks 10,20,30…; cdMult=1.
                //   Enemy:  Str=16 → MaxHealth=130, no Toughness, no skills.
                //   Tick 10: slot0 deals 10 then stacks Str→20; slot1 deals 20 → 30 (enemy 130→100).
                //   Tick 20: slot0 deals 20 then stacks Str→30; slot1 deals 30 → 50 (100→50).
                //   Tick 30: slot0 deals 30 then stacks Str→40; slot1 deals 40 → enemy 50→−20: dies at tick 1200.
                //   (If slot1 didn't see slot0's buff, tick 10 would deal only 20; refresh-only would end later.)
                ["sameTickEarlierSlotBuffsLaterSlot"] = new ParityScenario(
                    Player: () => MakeBattler(
                        strength: 10, endurance: 0,
                        skills:
                        [
                            MakeSkill(1, baseDamage: 0, cooldownMs: 400, mult: EAttribute.Strength, multAmount: 1.0,
                                effects: [MakeEffect(102, ESkillEffectTarget.Self, EAttribute.Strength, EModifierType.Additive, 10, Permanent)]),
                            MakeSkill(2, baseDamage: 0, cooldownMs: 400, mult: EAttribute.Strength, multAmount: 1.0),
                        ]),
                    Enemy: () => MakeEnemy(
                        strength: 16, endurance: 0,
                        skills: []),
                    ExpectedVictory: true,
                    ExpectedPlayerDied: false,
                    ExpectedTotalMs: 1200),

                // MaxHealth clamp: an Opponent MaxHealth ×0.5 debuff halves the enemy's maximum, and because its
                // current health exceeds the new max it is clamped down immediately — bringing the kill forward.
                // The debuff also STACKS on each fire, so MaxHealth keeps halving and the clamp bites repeatedly.
                //   Player: skill baseDamage 12, no multiplier → 12 per hit (no Toughness), cooldown 400 (fires tick 10,20…).
                //     Effect: Opponent ×0.5 MaxHealth (multiplicative), effectively permanent — a stack per fire.
                //   Enemy:  Str=10 → MaxHealth=100, no Toughness, no skills.
                //   Tick 10: deal 12 (100→88), stack ×0.5 → MaxHealth 50, clamp 88→50.
                //   Tick 20: deal 12 (50→38), stack → MaxHealth 25, clamp 38→25.
                //   Tick 30: deal 12 (25→13), stack → MaxHealth 12.5, clamp 13→12.5.
                //   Tick 40: deal 12 (12.5→0.5), stack → MaxHealth 6.25 (no clamp). Tick 50: deal 12 → dead at 2000.
                ["opponentMaxHealthDebuffClamps"] = new ParityScenario(
                    Player: () => MakeBattler(
                        strength: 0, endurance: 0,
                        skills:
                        [
                            MakeSkill(1, baseDamage: 12, cooldownMs: 400,
                                effects: [MakeEffect(103, ESkillEffectTarget.Opponent, EAttribute.MaxHealth, EModifierType.Multiplicative, 0.5, Permanent)]),
                        ]),
                    Enemy: () => MakeEnemy(
                        strength: 10, endurance: 0,
                        skills: []),
                    ExpectedVictory: true,
                    ExpectedPlayerDied: false,
                    ExpectedTotalMs: 2000),

                // MaxHealth debuff is LETHAL via the clamp (#1145): an Opponent MaxHealth additive −200 debuff
                // drives the enemy's maximum below zero, so the clamp pulls its current health to ≤ 0 and the
                // live IsDead getter reports death — a kill with NO direct damage, exercising the clamp path a
                // cached isDead flag (re-synced only at the damage mutations) would miss. This is the frontend
                // parity gap #1145 fixed by deriving isDead; the backend already derives IsDead so it passes here.
                //   Player: skill baseDamage 0, no multiplier, cooldown 400 → fires tick 10. Effect: Opponent
                //     −200 MaxHealth (additive), permanent.
                //   Enemy:  Str=10 → MaxHealth=100, no Toughness, no skills.
                //   Tick 10: 0 direct damage, then −200 MaxHealth → MaxHealth −100, clamp 100→−100 → dead at 400ms.
                ["maxHealthDebuffIsLethal"] = new ParityScenario(
                    Player: () => MakeBattler(
                        strength: 0, endurance: 0,
                        skills:
                        [
                            MakeSkill(1, baseDamage: 0, cooldownMs: 400,
                                effects: [MakeEffect(105, ESkillEffectTarget.Opponent, EAttribute.MaxHealth, EModifierType.Additive, -200, Permanent)]),
                        ]),
                    Enemy: () => MakeEnemy(strength: 10, endurance: 0, skills: []),
                    ExpectedVictory: true,
                    ExpectedPlayerDied: false,
                    ExpectedTotalMs: 400),

                // Same-tick CDR ordering: slot 0's self CooldownRecovery buff (applied after it fires) speeds
                // up slot 1's charge accrual ON THE SAME TICK, because each slot reads the cooldown multiplier
                // live in loadout order — so an earlier slot's effect influences a later slot firing that tick.
                // Slot 0 fires every tick and its buff STACKS, so cdMult climbs 1→2→3→… and slot 1's accrual
                // accelerates each tick.
                //   Player: base CDR=1 (cdMult=1). slot0 = pure buffer (0 damage, cooldown 40, Self +1.0 CDR
                //     permanent — a stack per tick); slot1 = baseDamage 27, cooldown 400, no multiplier.
                //   Enemy:  Str=5 → MaxHealth=75, no Toughness, no skills. Each slot1 hit deals 27 (no mitigation).
                //   cdMult after slot0's tick-n fire is n+1, so slot1 accrues 80,120,160,200,… and its charge
                //   passes 400 at tick 4 (80+120+160+200=560), then again at ticks 6 and 8 → three 27-dmg hits
                //   drop the 75-HP enemy at tick 8 → 320ms. (Refresh-only held cdMult=2 → 600.)
                ["cdrBuffSpeedsLaterSlotSameTick"] = new ParityScenario(
                    Player: () => MakeBattler(
                        strength: 0, endurance: 0,
                        skills:
                        [
                            MakeSkill(1, baseDamage: 0, cooldownMs: 40,
                                effects: [MakeEffect(104, ESkillEffectTarget.Self, EAttribute.CooldownRecovery, EModifierType.Additive, 1.0, Permanent)]),
                            MakeSkill(2, baseDamage: 27, cooldownMs: 400),
                        ]),
                    Enemy: () => MakeEnemy(
                        strength: 5, endurance: 0,
                        skills: []),
                    ExpectedVictory: true,
                    ExpectedPlayerDied: false,
                    ExpectedTotalMs: 320),

                // DoT kill timing: a constant poison on the enemy (a base PoisonDamagePerSecond debuff, no direct
                // damage) ticks it down. DTPS 50 → 50*40/1000 = 2 damage at the END of each tick, bypassing
                // mitigation. 2/tick over 25 ticks brings the 50-HP enemy to 0 → victory at tick 1000.
                ["poisonKillTiming"] = new ParityScenario(
                    Player: () => MakeBattler(strength: 0, endurance: 0, skills: []),
                    Enemy: () => MakeEnemy(strength: 0, endurance: 0, skills: [], extra: [(EAttribute.PoisonDamagePerSecond, 50)]),
                    ExpectedVictory: true,
                    ExpectedPlayerDied: false,
                    ExpectedTotalMs: 1000),

                // Same-tick mutual DoT tie favours the player: both battlers carry a constant PoisonDamagePerSecond
                // (base poison) for 2/tick and reach 0 HP on the same tick (1000). The end-of-tick phase resolves
                // the ENEMY first, so its death is awarded as a victory before the player's identical DoT is
                // applied — the player never takes the lethal tick. (Applying the player's DoT first would flip
                // this to a loss.)
                ["poisonMutualDotTieFavoursPlayer"] = new ParityScenario(
                    Player: () => MakeBattler(strength: 0, endurance: 0, skills: [], extra: [(EAttribute.PoisonDamagePerSecond, 50)]),
                    Enemy: () => MakeEnemy(strength: 0, endurance: 0, skills: [], extra: [(EAttribute.PoisonDamagePerSecond, 50)]),
                    ExpectedVictory: true,
                    ExpectedPlayerDied: false,
                    ExpectedTotalMs: 1000),

                // Heal-over-time offsets incoming damage and flips the outcome. The player carries a constant
                // HealthRegenPerSecond 75 (base regen) → 3 HP at each tick's end; the enemy chips 2/tick
                // (baseDamage 2, the player has no Toughness). The regen fully offsets the chip (capped at
                // MaxHealth), so the 50-HP player never dies, and dealing no damage back the battle runs to the
                // timeout. Without the regen the 2/tick would kill the player at tick 1000.
                ["regenOutpacesDamage"] = new ParityScenario(
                    Player: () => MakeBattler(strength: 0, endurance: 0, skills: [], extra: [(EAttribute.HealthRegenPerSecond, 75)]),
                    Enemy: () => MakeEnemy(
                        strength: 0, endurance: 0,
                        skills: [MakeSkill(2, baseDamage: 2, cooldownMs: 40)]),
                    ExpectedVictory: false,
                    ExpectedPlayerDied: false,
                    ExpectedTotalMs: GameConstants.DefaultMaxBattleMs),

                // Ordering within the end-of-tick phase: for each battler PoisonDamagePerSecond then
                // HealthRegenPerSecond apply BEFORE its death check, so the heal can offset a lethal DoT tick
                // (#1090). The player carries a constant 250 PoisonDamagePerSecond (10/tick) and 150
                // HealthRegenPerSecond (6/tick) — a net −4 grinding its 50 HP down. Each tick the 6 heal applies
                // after the 10 DoT and is checked together, so the player only dies once the net drain crosses 0:
                // 50/4 = 12.5 → death at tick 13 → 520ms. (Checking death before the heal would have killed it at
                // tick 11/440ms; see regenSavesFromLethalDot for a net-zero drain the heal fully survives.)
                ["dotRegenNetNegativeKillsAfterHeal"] = new ParityScenario(
                    Player: () => MakeBattler(
                        strength: 0, endurance: 0, skills: [],
                        extra: [(EAttribute.PoisonDamagePerSecond, 250), (EAttribute.HealthRegenPerSecond, 150)]),
                    Enemy: () => MakeEnemy(strength: 0, endurance: 0, skills: []),
                    ExpectedVictory: false,
                    ExpectedPlayerDied: true,
                    ExpectedTotalMs: 520),

                // Heal-over-time saves the battler from an otherwise-lethal DoT tick (#1090). The player carries
                // a constant 1500 PoisonDamagePerSecond (60/tick) — more than its whole 50 HP — and an equal 1500
                // HealthRegenPerSecond (60/tick). Each tick the 60 DoT drives HP to −10 but the same-tick 60 heal
                // (applied before the death check, capped at MaxHealth) restores it to 50, so the player never
                // dies; dealing no damage back, the battle runs to the timeout. (Checking death before the heal
                // would have killed it on tick 1.)
                ["regenSavesFromLethalDot"] = new ParityScenario(
                    Player: () => MakeBattler(
                        strength: 0, endurance: 0, skills: [],
                        extra: [(EAttribute.PoisonDamagePerSecond, 1500), (EAttribute.HealthRegenPerSecond, 1500)]),
                    Enemy: () => MakeEnemy(strength: 0, endurance: 0, skills: []),
                    ExpectedVictory: false,
                    ExpectedPlayerDied: false,
                    ExpectedTotalMs: GameConstants.DefaultMaxBattleMs),

                // DoT bypasses the Toughness curve. The enemy's Endurance 10 → Toughness 20 would halve a direct
                // hit against the level-1 player, but its constant 250 PoisonDamagePerSecond (base poison) →
                // 10/tick ignores mitigation and grinds the enemy's 250 HP down: victory at tick 1000. If DoT were
                // mitigated by Toughness (5/tick) the enemy would die at tick 2000 instead.
                ["dotBypassesMitigation"] = new ParityScenario(
                    Player: () => MakeBattler(
                        strength: 0, endurance: 0,
                        skills: [MakeSkill(1, baseDamage: 0, cooldownMs: 40)]),
                    Enemy: () => MakeEnemy(strength: 0, endurance: 10, skills: [], extra: [(EAttribute.PoisonDamagePerSecond, 250)]),
                    ExpectedVictory: true,
                    ExpectedPlayerDied: false,
                    ExpectedTotalMs: 1000),

                // DoT stops when its effect expires. A single poison application — the skill's 2000ms cooldown
                // fires it once at tick 50 — lasts 400ms = 10 ticks, dealing 2/tick for 20 total, leaving the
                // 50-HP enemy at 30. Capped at maxMs 3000 (before the skill could fire again at 4000) the battle
                // ends with no winner; had the DoT never expired the 2/tick would have killed the enemy by ~2960.
                ["dotExpiresStopsDamage"] = new ParityScenario(
                    Player: () => MakeBattler(
                        strength: 0, endurance: 0,
                        skills:
                        [
                            MakeSkill(1, baseDamage: 0, cooldownMs: 2000,
                                effects: [MakeEffect(207, ESkillEffectTarget.Opponent, EAttribute.PoisonDamagePerSecond, EModifierType.Additive, 50, 400)]),
                        ]),
                    Enemy: () => MakeEnemy(strength: 0, endurance: 0, skills: []),
                    ExpectedVictory: false,
                    ExpectedPlayerDied: false,
                    ExpectedTotalMs: 3000,
                    MaxMs: 3000),

                // Effect apply → expire → re-apply (#941): a periodic poison whose duration (80ms = 2 ticks) is
                // shorter than its skill's cooldown (120ms = 3 ticks), so each application fully LAPSES for a
                // tick before the skill fires again and re-applies — the cycle where the backend's absolute
                // ExpiresAtMs clock and the frontend's decrementing remainingMs are most likely to drift by a
                // tick. (Distinct from the refresh-while-active stacking and single-shot expiry rows.)
                //   Player: skill baseDamage 0, cooldown 120, Opponent +250 PoisonDamagePerSecond for 80ms → 10/tick.
                //     Fires at ticks 120,240,360; each application deals DoT on its 2 active ticks, then lapses one.
                //   Enemy:  MaxHealth 50, no skills.
                //   Active ticks 120,160 (50→30), lapse 200, re-apply 240,280 (30→10), lapse 320, re-apply 360
                //   (→0): dies at tick 360 → 360ms. (A non-lapsing constant 10/tick from tick 120 would kill at 280.)
                ["effectExpiresThenReapplies"] = new ParityScenario(
                    Player: () => MakeBattler(
                        strength: 0, endurance: 0,
                        skills:
                        [
                            MakeSkill(1, baseDamage: 0, cooldownMs: 120,
                                effects: [MakeEffect(210, ESkillEffectTarget.Opponent, EAttribute.PoisonDamagePerSecond, EModifierType.Additive, 250, 80)]),
                        ]),
                    Enemy: () => MakeEnemy(strength: 0, endurance: 0, skills: []),
                    ExpectedVictory: true,
                    ExpectedPlayerDied: false,
                    ExpectedTotalMs: 360),

                // DoT stacking: a poison re-applied every tick STACKS (each application adds another
                // PoisonDamagePerSecond debuff) instead of refreshing, so the per-tick damage ramps. The skill
                // (cooldown 40) applies Opponent +25 PoisonDamagePerSecond each tick; at tick n the enemy
                // carries n stacks = 25n DTPS → 25n×40/1000 = n damage that tick (bypassing mitigation).
                // Cumulative n(n+1)/2 reaches the 50-HP enemy's total on tick 10 (45 after tick 9, +10 = 55)
                // → victory at 400ms. (Refresh-only would hold 25 DTPS = 1/tick → 2000ms.)
                ["dotStacksEachApplication"] = new ParityScenario(
                    Player: () => MakeBattler(
                        strength: 0, endurance: 0,
                        skills:
                        [
                            MakeSkill(1, baseDamage: 0, cooldownMs: 40,
                                effects: [MakeEffect(208, ESkillEffectTarget.Opponent, EAttribute.PoisonDamagePerSecond, EModifierType.Additive, 25, Permanent)]),
                        ]),
                    Enemy: () => MakeEnemy(strength: 0, endurance: 0, skills: []),
                    ExpectedVictory: true,
                    ExpectedPlayerDied: false,
                    ExpectedTotalMs: 400),

                // Shared per-attribute expiry (#992 / #740): a poison whose duration (120ms = 3 ticks) outlasts
                // its skill's cooldown (80ms = 2 ticks), so each fire re-applies WHILE the previous applications
                // are still active — resetting the whole PoisonDamagePerSecond stack's single shared expiry to the
                // new duration. The reset always lands before the 3-tick duration would lapse, so nothing ever
                // expires and the stacks accumulate, the damage ramping like a permanent poison.
                //   Player: skill baseDamage 0, cooldown 80 → fires ticks 2,4,6; Opponent +250 PoisonDamagePerSecond, 120ms.
                //   Enemy:  Str=14 → MaxHealth=120, no Toughness, no skills. Each stack = 250 DTPS → 10/tick (bypassing mitigation).
                //   Stacks 1 (ticks 2-3), 2 (4-5), 3 (6-7); DoT/tick = 10×stacks. Cum 10,20,40,60,90,120 → the
                //   120-HP enemy dies on tick 7 → 280ms. (Independent per-application expiry — the bug this fixes —
                //   would let the older applications lapse each cycle, oscillating the stack count and killing at 400ms.)
                ["dotReapplyExtendsSharedExpiration"] = new ParityScenario(
                    Player: () => MakeBattler(
                        strength: 0, endurance: 0,
                        skills:
                        [
                            MakeSkill(1, baseDamage: 0, cooldownMs: 80,
                                effects: [MakeEffect(211, ESkillEffectTarget.Opponent, EAttribute.PoisonDamagePerSecond, EModifierType.Additive, 250, 120)]),
                        ]),
                    Enemy: () => MakeEnemy(strength: 14, endurance: 0, skills: []),
                    ExpectedVictory: true,
                    ExpectedPlayerDied: false,
                    ExpectedTotalMs: 280),

                // Effect magnitude scales with the CASTER's attribute (#741): a poison whose authored amount is
                // 0 but scales with the caster's Intellect at 1.0/point. Intellect feeds no derived attribute,
                // so it perturbs nothing but this scaling — keeping the timing hand-computable.
                //   Player: Intellect=50; skill baseDamage 0, cooldown 2000 → fires once at tick 50 (cdMult=1).
                //     Effect: Opponent +PoisonDamagePerSecond, base 0 + Intellect(50)×1.0 = 50 DTPS, permanent.
                //   Enemy:  Str=0, End=0 → MaxHealth=50, no skills. DoT (bypassing mitigation) = 50×40/1000 = 2/tick.
                //   From tick 50 the enemy loses 2/tick; 50 HP / 2 = 25 ticks → dies on tick 74 → 2960ms.
                //   (Without scaling the authored 0 would deal nothing and the battle would never resolve.)
                ["effectScalesWithCasterIntellect"] = new ParityScenario(
                    Player: () => MakeBattler(
                        strength: 0, endurance: 0,
                        skills:
                        [
                            MakeSkill(1, baseDamage: 0, cooldownMs: 2000,
                                effects: [MakeEffect(209, ESkillEffectTarget.Opponent, EAttribute.PoisonDamagePerSecond, EModifierType.Additive, 0, Permanent,
                                    scalingAttribute: EAttribute.Intellect, scalingAmount: 1.0)]),
                        ],
                        extra: [(EAttribute.Intellect, 50)]),
                    Enemy: () => MakeEnemy(strength: 0, endurance: 0, skills: []),
                    ExpectedVictory: true,
                    ExpectedPlayerDied: false,
                    ExpectedTotalMs: 2960),

                // ── Seeded crit/dodge (player-only) ──────────────────────────────────────────
                // Each chance is forced to 1 or 0 so the outcome is deterministic regardless of the seed (a
                // chance ≥ 1 always succeeds against a [0,1) draw, a chance ≤ 0 never does). The draws are still
                // taken in lockstep on both sides, so these pin the player-only crit/dodge math and draw order
                // while staying hand-computable.

                // A forced crit (CriticalChance 1) multiplies the raw damage by CriticalDamage BEFORE mitigation.
                // CriticalDamage is the base 1.5 (sourced by #799) plus a 0.5 allocation = 2.0 total: 20 raw × 2
                // = 40/hit (the enemy has no Toughness), so the 100-HP enemy dies on hit 3 at tick 30 → 1200ms
                // (vs hit 5 / 2000ms at the un-critted 20/hit).
                ["forcedCrit"] = new ParityScenario(
                    Player: () => MakeBattler(
                        strength: 10, endurance: 0,
                        skills: [MakeSkill(1, baseDamage: 20, cooldownMs: 400)],
                        extra: [(EAttribute.CriticalChance, 1.0), (EAttribute.CriticalDamage, 0.5)]),
                    Enemy: () => MakeEnemy(strength: 10, endurance: 0, skills: []),
                    ExpectedVictory: true,
                    ExpectedPlayerDied: false,
                    ExpectedTotalMs: 1200),

                // A forced dodge (DodgeChance 1) negates every incoming hit: the enemy's 1000-damage skill would
                // one-shot the 100-HP player at tick 10, but each hit deals 0, so the player survives and grinds
                // the enemy down (20/hit, no Toughness) for the win on hit 5 at tick 50 → 2000ms.
                ["forcedDodge"] = new ParityScenario(
                    Player: () => MakeBattler(
                        strength: 10, endurance: 0,
                        skills: [MakeSkill(1, baseDamage: 20, cooldownMs: 400)],
                        extra: [(EAttribute.DodgeChance, 1.0)]),
                    Enemy: () => MakeEnemy(
                        strength: 10, endurance: 0,
                        skills: [MakeSkill(2, baseDamage: 1000, cooldownMs: 400)]),
                    ExpectedVictory: true,
                    ExpectedPlayerDied: false,
                    ExpectedTotalMs: 2000),

                // Draw-order alignment over a multi-skill exchange: two player skills (two crit draws) and two
                // enemy skills (two dodge draws, one each now that Block's second draw is gone) fire on the same
                // ticks. CriticalDamage folds in the #799 base (1.5 + 0.5 = 2 multiplier). Neither side has
                // Toughness. The player crits both hits (10×2=20, 15×2=30 → 50/tick); the enemy's two hits land
                // in full (12 + 14 = 26/tick — no Block). The 100-HP enemy dies on the player's tick-20 volley →
                // 800ms (before its own tick-20 attack), so the player only takes the tick-10 volley and ends at 74 HP.
                ["drawOrderMultiSkill"] = new ParityScenario(
                    Player: () => MakeBattler(
                        strength: 10, endurance: 0,
                        skills:
                        [
                            MakeSkill(1, baseDamage: 10, cooldownMs: 400),
                            MakeSkill(2, baseDamage: 15, cooldownMs: 400),
                        ],
                        extra: [(EAttribute.CriticalChance, 1.0), (EAttribute.CriticalDamage, 0.5)]),
                    Enemy: () => MakeEnemy(
                        strength: 10, endurance: 0,
                        skills:
                        [
                            MakeSkill(3, baseDamage: 12, cooldownMs: 400),
                            MakeSkill(4, baseDamage: 14, cooldownMs: 400),
                        ]),
                    ExpectedVictory: true,
                    ExpectedPlayerDied: false,
                    ExpectedTotalMs: 800),

                // Enemies never crit/dodge — even with every chance forced to 1, the gating reads the rolls only
                // on the player's side. The enemy's forced crit is ignored (it deals 20, not 40, so the player
                // survives to win) and its forced dodge is ignored (irrelevant on offence). The player's
                // 20-damage hits land in full and kill it on hit 5 → 2000ms.
                ["enemyForcedChanceIgnored"] = new ParityScenario(
                    Player: () => MakeBattler(
                        strength: 10, endurance: 0,
                        skills: [MakeSkill(1, baseDamage: 20, cooldownMs: 400)]),
                    Enemy: () => MakeEnemy(
                        strength: 10, endurance: 0,
                        skills: [MakeSkill(2, baseDamage: 20, cooldownMs: 400)],
                        extra:
                        [
                            (EAttribute.CriticalChance, 1.0), (EAttribute.CriticalDamage, 2.0),
                            (EAttribute.DodgeChance, 1.0),
                        ]),
                    ExpectedVictory: true,
                    ExpectedPlayerDied: false,
                    ExpectedTotalMs: 2000),

                // ── Deterministic damage reflection (spike #1330) ────────────────────────────
                // Reflection returns the defender's DamageReflection share of a direct hit's net damage to the
                // attacker, bypassing the attacker's mitigation. Authored-only (granted here as a raw attribute),
                // deterministic (no draw), and scoped to direct hits. Hand-computable; mirrored in the frontend suite.

                // Reflection as a kill condition: a pure tank (no skills) returns 100% of every hit it takes.
                // The player has no Toughness (MaxHealth 550 from Strength, Endurance 0), so it takes the enemy's
                // full 25 and reflects 25 back each hit. The 100-HP enemy dies to its own reflected damage on its
                // 4th attack (tick 40 → 1600ms) — exercising the attacker-death-from-reflection check after the
                // enemy's turn — while the 550-HP player (down to 450) easily survives.
                ["reflectionKillsAttacker"] = new ParityScenario(
                    Player: () => MakeBattler(
                        strength: 100, endurance: 0, skills: [],
                        extra: [(EAttribute.DamageReflection, 1.0)]),
                    Enemy: () => MakeEnemy(
                        strength: 10, endurance: 0,
                        skills: [MakeSkill(1, baseDamage: 25, cooldownMs: 400)]),
                    ExpectedVictory: true,
                    ExpectedPlayerDied: false,
                    ExpectedTotalMs: 1600),

                // Reflection ignores DoT: the player bears a constant 50 PoisonDamagePerSecond (2/tick, self-
                // inflicted) and 100% DamageReflection but no skills, against a 100-HP enemy with no skills. DoT
                // is applied through the end-of-tick phase, not a direct hit, so it is never reflected — the enemy
                // takes nothing back and never dies, and the player's own poison grinds its 200 HP (Str 30) down,
                // killing it on tick 100 → 4000ms. (Were DoT reflected, the enemy would die at tick 50 / 2000ms.)
                ["reflectionIgnoresDot"] = new ParityScenario(
                    Player: () => MakeBattler(
                        strength: 30, endurance: 0, skills: [],
                        extra: [(EAttribute.PoisonDamagePerSecond, 50), (EAttribute.DamageReflection, 1.0)]),
                    Enemy: () => MakeEnemy(strength: 10, endurance: 0, skills: []),
                    ExpectedVictory: false,
                    ExpectedPlayerDied: true,
                    ExpectedTotalMs: 4000),

                // Simplified draw order — an enemy attack now draws ONE dodge value (not dodge + block), so the
                // player's fractional crit draws interleave at EVEN stream positions (the enemy's single dodge
                // draw sits between consecutive player crit draws). With a real CriticalChance 0.5 against
                // ParitySeed the crit draws at stream indices 0, 2, 4, 6 are crit, no, crit, crit; CriticalDamage
                // base 1.5 + 0.5 = 2.0, so the player deals 24, 12, 24, 24 (no Toughness) for a cumulative
                // 24, 36, 60, 84. The 80-HP enemy (Str 6) dies on the tick-40 fire → 1600ms. Had the enemy still
                // drawn TWICE (the old dodge + block), the crit draws would land at indices 0, 3, 6, 9 —
                // crit, no, crit, no → 24, 12, 24, 12 (cumulative 24, 36, 60, 72) — pushing the kill to tick 50
                // (2000ms). So this row pins the one-draw enemy attack. The enemy chips 5/tick (DodgeChance 0, so
                // its dodge draw is taken but never succeeds), leaving the 100-HP player at 85.
                ["drawOrderDodgeOnlyAlignsCrit"] = new ParityScenario(
                    Player: () => MakeBattler(
                        strength: 10, endurance: 0,
                        skills: [MakeSkill(1, baseDamage: 12, cooldownMs: 400)],
                        extra: [(EAttribute.CriticalChance, 0.5), (EAttribute.CriticalDamage, 0.5)]),
                    Enemy: () => MakeEnemy(
                        strength: 6, endurance: 0,
                        skills: [MakeSkill(2, baseDamage: 5, cooldownMs: 400)]),
                    ExpectedVictory: true,
                    ExpectedPlayerDied: false,
                    ExpectedTotalMs: 1600),

                // Fractional crit chance against a fixed seed (#941): unlike the forced-1/0 crit rows, CriticalChance
                // is a real 0.5 fraction, so whether each player fire crits depends on the actual Mulberry32 [0,1)
                // draw — validating that a fractional chance compared against a real draw lands identically on both
                // ports (not just the draw ordering/math). With ParitySeed the first six crit draws are
                // crit,crit,no,no,crit,crit.
                //   Player: skill baseDamage 12, cooldown 400 (one crit draw per fire, every 10 ticks); CriticalDamage
                //     base 1.5 + 0.5 = 2.0 → a crit deals 12×2 = 24, a non-crit 12 (the enemy has no Toughness).
                //   Enemy:  Str 10 → MaxHealth 100, no Toughness, no skills (no enemy draws, so the stream is crit draws only).
                //   Hits 24,24,12,12,24 (cum 96) then the 6th draw's crit (+24 → 120 ≥ 100) kills on fire 6 → 2400ms.
                //   The 6th hit being a crit is decisive: a non-crit there (+12 → 108) would still kill on fire 6 here.
                ["fractionalCritChance"] = new ParityScenario(
                    Player: () => MakeBattler(
                        strength: 0, endurance: 0,
                        skills: [MakeSkill(1, baseDamage: 12, cooldownMs: 400)],
                        extra: [(EAttribute.CriticalChance, 0.5), (EAttribute.CriticalDamage, 0.5)]),
                    Enemy: () => MakeEnemy(strength: 10, endurance: 0, skills: []),
                    ExpectedVictory: true,
                    ExpectedPlayerDied: false,
                    ExpectedTotalMs: 2400),

                // ── Item-granted skills (append + order + dedupe) ─────────────────────────────
                // The player's battler fields its selected skills plus the skills its equipped items grant
                // (BattleSnapshot.ToBattler appends them, de-duplicated, in EEquipmentSlot order). Each scenario
                // is built through the snapshot path with an equipped item carrying a GrantedSkillId.

                // An item grants a skill in addition to a selected one: both fire. Selected S1 (20/hit) and
                // granted S2 (30/hit) — the enemy has no Toughness — on cooldown 400 fire together (ticks 10,20)
                // for 50/tick; the 100-HP enemy dies on the tick-20 volley → 800ms. (The selected skill alone —
                // 20/tick — would win only on hit 5 at 2000ms, so the grant is decisive.)
                ["itemGrantsSkill"] = new ParityScenario(
                    Player: () => MakeBattlerWithGrants(
                        strength: 10,
                        selectedSkillIds: [1],
                        grants: [(100, 2)],
                        skillPool:
                        [
                            MakeSkill(1, baseDamage: 20, cooldownMs: 400),
                            MakeSkill(2, baseDamage: 30, cooldownMs: 400),
                        ]),
                    Enemy: () => MakeEnemy(strength: 10, endurance: 0, skills: []),
                    ExpectedVictory: true,
                    ExpectedPlayerDied: false,
                    ExpectedTotalMs: 800),

                // Two equipped items grant the SAME skill: it is de-duplicated to one (not fielded twice). The
                // single granted S2 deals 30/hit (no Toughness, cooldown 400); the 100-HP enemy dies on hit 4 at
                // tick 40 → 1600ms. (Two un-deduped copies — 60/tick — would kill on hit 2 at 800ms.)
                ["twoItemsGrantSameSkill"] = new ParityScenario(
                    Player: () => MakeBattlerWithGrants(
                        strength: 10,
                        selectedSkillIds: [],
                        grants: [(100, 2), (101, 2)],
                        skillPool: [MakeSkill(2, baseDamage: 30, cooldownMs: 400)]),
                    Enemy: () => MakeEnemy(strength: 10, endurance: 0, skills: []),
                    ExpectedVictory: true,
                    ExpectedPlayerDied: false,
                    ExpectedTotalMs: 1600),

                // A granted skill duplicating a SELECTED skill is de-duplicated (first/selected wins): the player
                // fields S1 once (22/hit, no Toughness, cooldown 400). The 100-HP enemy dies on hit 5 at tick 50 →
                // 2000ms. (Without dedupe the two copies — 44/tick — would kill on hit 3 at 1200ms.)
                ["grantedSkillDuplicatesSelected"] = new ParityScenario(
                    Player: () => MakeBattlerWithGrants(
                        strength: 10,
                        selectedSkillIds: [1],
                        grants: [(100, 1)],
                        skillPool: [MakeSkill(1, baseDamage: 22, cooldownMs: 400)]),
                    Enemy: () => MakeEnemy(strength: 10, endurance: 0, skills: []),
                    ExpectedVictory: true,
                    ExpectedPlayerDied: false,
                    ExpectedTotalMs: 2000),

                // The SAME skill selected twice is de-duplicated WITHIN the selected loadout (first wins): the
                // player fields S1 once (22/hit, no Toughness, cooldown 400). The 100-HP enemy dies on hit 5 at
                // tick 50 → 2000ms. (Without intra-selected dedupe the two copies — 44/tick — would kill on hit 3
                // at 1200ms.) Pins the single Distinct() over the full loadout that the frontend mirrors.
                ["duplicateSelectedSkill"] = new ParityScenario(
                    Player: () => MakeBattlerWithGrants(
                        strength: 10,
                        selectedSkillIds: [1, 1],
                        grants: [],
                        skillPool: [MakeSkill(1, baseDamage: 22, cooldownMs: 400)]),
                    Enemy: () => MakeEnemy(strength: 10, endurance: 0, skills: []),
                    ExpectedVictory: true,
                    ExpectedPlayerDied: false,
                    ExpectedTotalMs: 2000),

                // A granted skill carrying an EFFECT works exactly as a selected one would: the item grants a
                // skill whose self +10 Strength buff stacks each fire, ramping its own damage. Str×1.0 raw (no
                // Toughness) deals 10,20,30,40,50 (Str climbing 10→50); cumulative 10,30,60,100,150 drops the
                // 150-HP enemy on fire 5 at tick 50 → 2000ms — proving granted skills wrap into a full BattleSkill.
                ["grantedSkillWithEffects"] = new ParityScenario(
                    Player: () => MakeBattlerWithGrants(
                        strength: 10,
                        selectedSkillIds: [],
                        grants: [(100, 2)],
                        skillPool:
                        [
                            MakeSkill(2, baseDamage: 0, cooldownMs: 400, mult: EAttribute.Strength, multAmount: 1.0,
                                effects: [MakeEffect(110, ESkillEffectTarget.Self, EAttribute.Strength, EModifierType.Additive, 10, Permanent)]),
                        ]),
                    Enemy: () => MakeEnemy(strength: 20, endurance: 0, skills: []),
                    ExpectedVictory: true,
                    ExpectedPlayerDied: false,
                    ExpectedTotalMs: 2000),

                // ── Direct-hit damage typing: amplification / resistance (#1320 Area B) ──────────
                // Skills carry a leaf damage type; the attacker's amplification and defender's resistance for
                // that type's applies() keys multiply the hit as separate budgets — × (1 + amp), × (1 − res),
                // res unclamped — with crit attacker-side (pre-mitigation) and the Toughness curve last. The
                // enemies here have no Toughness, so the outcomes are hand-computable. Mirrored in the frontend suite.

                // Attacker amplification: a Fire skill with +0.5 FireAmplification deals 20 × 1.5 = 30/hit (no
                // Toughness), so the 100-HP enemy dies on hit 4 at tick 40 → 1600ms (vs hit 5 / 2000ms un-amplified).
                ["fireAmplification"] = new ParityScenario(
                    Player: () => MakeBattler(
                        strength: 10, endurance: 0,
                        skills: [MakeSkill(1, baseDamage: 20, cooldownMs: 400, damageType: EDamageType.Fire)],
                        extra: [(EAttribute.FireAmplification, 0.5)]),
                    Enemy: () => MakeEnemy(strength: 10, endurance: 0, skills: []),
                    ExpectedVictory: true,
                    ExpectedPlayerDied: false,
                    ExpectedTotalMs: 1600),

                // Defender resistance: the enemy's +0.5 FireResistance halves a 40-damage Fire hit to 20/hit (no
                // Toughness), so the 100-HP enemy dies on hit 5 at tick 50 → 2000ms (vs hit 3 / 1200ms un-resisted).
                ["fireResistance"] = new ParityScenario(
                    Player: () => MakeBattler(
                        strength: 10, endurance: 0,
                        skills: [MakeSkill(1, baseDamage: 40, cooldownMs: 400, damageType: EDamageType.Fire)]),
                    Enemy: () => MakeEnemy(
                        strength: 10, endurance: 0, skills: [],
                        extra: [(EAttribute.FireResistance, 0.5)]),
                    ExpectedVictory: true,
                    ExpectedPlayerDied: false,
                    ExpectedTotalMs: 2000),

                // Vulnerability (res < 0): a −1.0 FireResistance doubles the incoming Fire hit (factor 1 − (−1) =
                // 2). A 20-damage hit becomes 40/hit (no Toughness), so the 100-HP enemy dies on hit 3 at tick 30 →
                // 1200ms (vs hit 5 / 2000ms at res 0). Resistance is deliberately left unclamped.
                ["fireVulnerability"] = new ParityScenario(
                    Player: () => MakeBattler(
                        strength: 10, endurance: 0,
                        skills: [MakeSkill(1, baseDamage: 20, cooldownMs: 400, damageType: EDamageType.Fire)]),
                    Enemy: () => MakeEnemy(
                        strength: 10, endurance: 0, skills: [],
                        extra: [(EAttribute.FireResistance, -1.0)]),
                    ExpectedVictory: true,
                    ExpectedPlayerDied: false,
                    ExpectedTotalMs: 1200),

                // Absorption (res > 1): a +2.0 FireResistance drives the post-resistance hit negative (20 × (1 −
                // 2) = −20), so an absorbed hit deals no damage — the Toughness curve never applies, and the heal
                // is capped at MaxHealth (the enemy is already full, so it stays at 100). The enemy never loses
                // health and never dies; dealing no damage back, the battle runs to the timeout. (At res 0 the
                // player's 20/hit would win by 2000ms.)
                ["fireAbsorption"] = new ParityScenario(
                    Player: () => MakeBattler(
                        strength: 0, endurance: 0,
                        skills: [MakeSkill(1, baseDamage: 20, cooldownMs: 400, damageType: EDamageType.Fire)]),
                    Enemy: () => MakeEnemy(
                        strength: 10, endurance: 0, skills: [],
                        extra: [(EAttribute.FireResistance, 2.0)]),
                    ExpectedVictory: false,
                    ExpectedPlayerDied: false,
                    ExpectedTotalMs: GameConstants.DefaultMaxBattleMs),

                // Crit multiplies pre-mitigation, so it punches through resistance AND the Toughness curve: a
                // forced crit (CriticalDamage base 1.5 + 0.5 = 2.0) on a Fire hit. The enemy's Toughness 20 vs the
                // level-1 player halves the post-resistance hit, so a normal hit deals 20 × (1 − 0.5 res) × 0.5 =
                // 5 while the crit deals 20 × 2 × 0.5 × 0.5 = 10/hit — dropping the 50-HP enemy on hit 5 at tick
                // 50 → 2000ms (a non-crit 5/hit would take 10 hits).
                ["critPunchesThroughTyped"] = new ParityScenario(
                    Player: () => MakeBattler(
                        strength: 0, endurance: 0,
                        skills: [MakeSkill(1, baseDamage: 20, cooldownMs: 400, damageType: EDamageType.Fire)],
                        extra: [(EAttribute.CriticalChance, 1.0), (EAttribute.CriticalDamage, 0.5)]),
                    Enemy: () => MakeEnemy(
                        strength: 0, endurance: 0, skills: [],
                        extra: [(EAttribute.Toughness, 20.0), (EAttribute.FireResistance, 0.5)]),
                    ExpectedVictory: true,
                    ExpectedPlayerDied: false,
                    ExpectedTotalMs: 2000),

                // Reduce-to-today identity: a Fire-typed skill with no amplification or resistance authored
                // anywhere, against an enemy with no Toughness — × (1 + 0), × (1 − 0) and the toughness factor are
                // all exact 1.0. 20 raw → 20/hit, so the 100-HP enemy dies on hit 5 → 2000ms, identical to a
                // baseDamage-20 physical skill against the same enemy.
                ["typedFireNoModifiersUnchanged"] = new ParityScenario(
                    Player: () => MakeBattler(
                        strength: 10, endurance: 0,
                        skills: [MakeSkill(1, baseDamage: 20, cooldownMs: 400, damageType: EDamageType.Fire)]),
                    Enemy: () => MakeEnemy(strength: 10, endurance: 0, skills: []),
                    ExpectedVictory: true,
                    ExpectedPlayerDied: false,
                    ExpectedTotalMs: 2000),

                // Bidirectional: the ENEMY amplifies and the PLAYER resists. The enemy's +1.0 FireAmplification
                // doubles its 20-damage Fire skill to 40; the player's +0.5 FireResistance halves that to 20/hit
                // (the player has no Toughness). The player (no skills) dies on the enemy's hit 5 at tick 50 →
                // 2000ms. (Without the enemy amp it would be 10/hit → slower; without the player resist, 40/hit.)
                ["enemyAmplifiesPlayerResists"] = new ParityScenario(
                    Player: () => MakeBattler(
                        strength: 10, endurance: 0, skills: [],
                        extra: [(EAttribute.FireResistance, 0.5)]),
                    Enemy: () => MakeEnemy(
                        strength: 10, endurance: 0,
                        skills: [MakeSkill(2, baseDamage: 20, cooldownMs: 400, damageType: EDamageType.Fire)],
                        extra: [(EAttribute.FireAmplification, 1.0)]),
                    ExpectedVictory: false,
                    ExpectedPlayerDied: true,
                    ExpectedTotalMs: 2000),

                // ── Typed damage-over-time: per-type accumulators + live resistance (#1320 Area C) ─────────
                // DoT is encoded by which per-second accumulator an effect targets; the end-of-tick phase loops
                // the DoT types in fixed order, applying each type's resistance SAMPLED LIVE off the bearer and
                // bypassing mitigation. A bearer is the defender of its own DoT, so an enemy's own resistance
                // mitigates its own poison. All hand-computable. Mirrored in the frontend suite.

                // DoT resistance slows the kill: the enemy's +0.5 PoisonResistance halves its constant 50
                // PoisonDamagePerSecond (2/tick → 1/tick), so the 50-HP enemy dies on tick 50 → 2000ms (vs the
                // un-resisted 1000ms of poisonKillTiming).
                ["poisonResistanceSlowsKill"] = new ParityScenario(
                    Player: () => MakeBattler(strength: 0, endurance: 0, skills: []),
                    Enemy: () => MakeEnemy(
                        strength: 0, endurance: 0, skills: [],
                        extra: [(EAttribute.PoisonDamagePerSecond, 50), (EAttribute.PoisonResistance, 0.5)]),
                    ExpectedVictory: true,
                    ExpectedPlayerDied: false,
                    ExpectedTotalMs: 2000),

                // DoT vulnerability (res < 0) hastens the kill: a −1.0 PoisonResistance doubles the constant 50
                // PoisonDamagePerSecond (2/tick → 4/tick), so the 50-HP enemy dies on tick 13 → 520ms.
                // Resistance is deliberately left unclamped.
                ["poisonVulnerabilityHastensKill"] = new ParityScenario(
                    Player: () => MakeBattler(strength: 0, endurance: 0, skills: []),
                    Enemy: () => MakeEnemy(
                        strength: 0, endurance: 0, skills: [],
                        extra: [(EAttribute.PoisonDamagePerSecond, 50), (EAttribute.PoisonResistance, -1.0)]),
                    ExpectedVictory: true,
                    ExpectedPlayerDied: false,
                    ExpectedTotalMs: 520),

                // DoT absorption (res > 1): a +2.0 PoisonResistance drives the tick negative (2 × (1 − 2) = −2),
                // so the enemy's own poison heals it instead of hurting — it never dies, and dealing no damage
                // back the battle runs to the timeout. (At res 0 it would die by 1000ms.)
                ["poisonAbsorptionPreventsDeath"] = new ParityScenario(
                    Player: () => MakeBattler(strength: 0, endurance: 0, skills: []),
                    Enemy: () => MakeEnemy(
                        strength: 0, endurance: 0, skills: [],
                        extra: [(EAttribute.PoisonDamagePerSecond, 50), (EAttribute.PoisonResistance, 2.0)]),
                    ExpectedVictory: false,
                    ExpectedPlayerDied: false,
                    ExpectedTotalMs: GameConstants.DefaultMaxBattleMs),

                // Multi-type DoT sums every accumulator in fixed order: Bleed 50 (2/tick) + Poison 100 (4/tick)
                // + Burn 25 (1/tick) = 7/tick, no resistance authored. The 350-HP enemy (Str 60) dies on tick
                // 50 → 2000ms.
                ["multiTypeDotSum"] = new ParityScenario(
                    Player: () => MakeBattler(strength: 0, endurance: 0, skills: []),
                    Enemy: () => MakeEnemy(
                        strength: 60, endurance: 0, skills: [],
                        extra:
                        [
                            (EAttribute.BleedDamagePerSecond, 50),
                            (EAttribute.PoisonDamagePerSecond, 100),
                            (EAttribute.BurnDamagePerSecond, 25),
                        ]),
                    ExpectedVictory: true,
                    ExpectedPlayerDied: false,
                    ExpectedTotalMs: 2000),

                // Reduce-to-today identity: a single typed DoT with no resistance behaves exactly like the
                // former single accumulator. A constant 50 BurnDamagePerSecond → 2/tick kills the 50-HP enemy on
                // tick 25 → 1000ms, byte-identical to poisonKillTiming's old DamageTakenPerSecond outcome.
                ["typedBurnNoResistanceUnchanged"] = new ParityScenario(
                    Player: () => MakeBattler(strength: 0, endurance: 0, skills: []),
                    Enemy: () => MakeEnemy(
                        strength: 0, endurance: 0, skills: [],
                        extra: [(EAttribute.BurnDamagePerSecond, 50)]),
                    ExpectedVictory: true,
                    ExpectedPlayerDied: false,
                    ExpectedTotalMs: 1000),

                // Cross-cutting resistance: burn resists as burn + fire + elemental + dot, so the enemy's +0.5
                // FireResistance halves its constant 250 BurnDamagePerSecond (10/tick → 5/tick) for free — the
                // 50-HP enemy dies on tick 10 → 400ms (vs 200ms with no fire resistance).
                ["burnResistedByFireResistance"] = new ParityScenario(
                    Player: () => MakeBattler(strength: 0, endurance: 0, skills: []),
                    Enemy: () => MakeEnemy(
                        strength: 0, endurance: 0, skills: [],
                        extra: [(EAttribute.BurnDamagePerSecond, 250), (EAttribute.FireResistance, 0.5)]),
                    ExpectedVictory: true,
                    ExpectedPlayerDied: false,
                    ExpectedTotalMs: 400),

                // Caster amplification is FROZEN into the accumulator at apply time: the player's +1.0
                // PoisonAmplification doubles the poison its skill applies (base 25 → 50 PoisonDamagePerSecond →
                // 2/tick). Fired once at tick 50 (cooldown 2000), the 50-HP enemy then loses 2/tick and dies on
                // tick 74 → 2960ms. (Without the amplification, 25 → 1/tick would not kill within one application.)
                ["poisonCasterAmplificationFreezes"] = new ParityScenario(
                    Player: () => MakeBattler(
                        strength: 0, endurance: 0,
                        skills:
                        [
                            MakeSkill(1, baseDamage: 0, cooldownMs: 2000,
                                effects: [MakeEffect(212, ESkillEffectTarget.Opponent, EAttribute.PoisonDamagePerSecond, EModifierType.Additive, 25, Permanent)]),
                        ],
                        extra: [(EAttribute.PoisonAmplification, 1.0)]),
                    Enemy: () => MakeEnemy(strength: 0, endurance: 0, skills: []),
                    ExpectedVictory: true,
                    ExpectedPlayerDied: false,
                    ExpectedTotalMs: 2960),

                // Resistance is sampled LIVE, not frozen: the enemy carries a constant 50 PoisonDamagePerSecond
                // (2/tick) and the player applies a permanent −4.0 PoisonResistance vulnerability debuff once at
                // tick 5 (cooldown 200). The first four ticks deal 2 (50→42); from tick 5 the live-sampled
                // resistance (factor 1 − (−4) = 5) makes the same poison deal 10/tick, killing the enemy on tick
                // 9 → 360ms — before the skill's tick-10 re-fire, so a single debuff application suffices. (A
                // frozen resistance would leave the poison at 2/tick → 1000ms.)
                ["poisonVulnerabilityDebuffSampledLive"] = new ParityScenario(
                    Player: () => MakeBattler(
                        strength: 0, endurance: 0,
                        skills:
                        [
                            MakeSkill(1, baseDamage: 0, cooldownMs: 200,
                                effects: [MakeEffect(213, ESkillEffectTarget.Opponent, EAttribute.PoisonResistance, EModifierType.Additive, -4.0, Permanent)]),
                        ]),
                    Enemy: () => MakeEnemy(
                        strength: 0, endurance: 0, skills: [],
                        extra: [(EAttribute.PoisonDamagePerSecond, 50)]),
                    ExpectedVictory: true,
                    ExpectedPlayerDied: false,
                    ExpectedTotalMs: 360),

                // ── Multi-typed direct hits: weighted portion split (#1343 / #1385) ──────────
                // A skill's raw is split across weighted portions (raw × weight ÷ Σweights), each run through the
                // single-type pipeline under its own type and the nets summed. A single crit multiplies every
                // portion; a single dodge zeroes the whole hit. All hand-computable; mirrored in the frontend suite.

                // 60/40 split with resistance on only one type. A raw-100 hit splits into 60 Physical + 40 Fire;
                // the enemy resists Fire 0.5 (Physical unresisted), no Toughness: 60 + 40×0.5 = 80/hit. The 250-HP
                // enemy (Str 40) dies on hit 4 at tick 40 → 1600ms. (Un-resisted 100/hit would kill on hit 3 / 1200ms,
                // so the per-portion Fire resistance is decisive.)
                ["multiTypeSplitResistsOneType"] = new ParityScenario(
                    Player: () => MakeBattler(
                        strength: 10, endurance: 0,
                        skills: [MakeMultiTypeSkill(1, baseDamage: 100, cooldownMs: 400,
                            [(EDamageType.Physical, 60), (EDamageType.Fire, 40)])]),
                    Enemy: () => MakeEnemy(
                        strength: 40, endurance: 0, skills: [],
                        extra: [(EAttribute.FireResistance, 0.5)]),
                    ExpectedVictory: true,
                    ExpectedPlayerDied: false,
                    ExpectedTotalMs: 1600),

                // A single crit multiplies EVERY portion. Forced crit (CriticalDamage 1.5 + 0.5 = 2) on a [Physical
                // 50, Fire 50] split of raw 20 → 10 each portion, ×2 → 20 each = 40/hit (no Toughness). The 100-HP
                // enemy dies on hit 3 at tick 30 → 1200ms. A crit applied to only one portion (30/hit) would kill on
                // hit 4 (1600ms), and no crit (20/hit) on hit 5 (2000ms) — so 1200ms pins the crit hitting both.
                ["multiTypeCritAppliesToAllPortions"] = new ParityScenario(
                    Player: () => MakeBattler(
                        strength: 0, endurance: 0,
                        skills: [MakeMultiTypeSkill(1, baseDamage: 20, cooldownMs: 400,
                            [(EDamageType.Physical, 50), (EDamageType.Fire, 50)])],
                        extra: [(EAttribute.CriticalChance, 1.0), (EAttribute.CriticalDamage, 0.5)]),
                    Enemy: () => MakeEnemy(strength: 10, endurance: 0, skills: []),
                    ExpectedVictory: true,
                    ExpectedPlayerDied: false,
                    ExpectedTotalMs: 1200),

                // A single dodge zeroes the WHOLE multi-typed hit. The enemy's [Physical 500, Fire 500] split of a
                // 1000-damage hit would one-shot the 100-HP player, but the player's DodgeChance 1 negates every
                // hit, so it survives and grinds the enemy down (20/hit, no Toughness) for the win on hit 5 at tick
                // 50 → 2000ms. (Zeroing only one portion would leave 500 through and kill the player at tick 10.)
                ["multiTypeDodgeZeroesWholeHit"] = new ParityScenario(
                    Player: () => MakeBattler(
                        strength: 10, endurance: 0,
                        skills: [MakeSkill(1, baseDamage: 20, cooldownMs: 400)],
                        extra: [(EAttribute.DodgeChance, 1.0)]),
                    Enemy: () => MakeEnemy(
                        strength: 10, endurance: 0,
                        skills: [MakeMultiTypeSkill(2, baseDamage: 1000, cooldownMs: 400,
                            [(EDamageType.Physical, 500), (EDamageType.Fire, 500)])]),
                    ExpectedVictory: true,
                    ExpectedPlayerDied: false,
                    ExpectedTotalMs: 2000),

                // Per-portion vulnerability (res < 0). A [Physical 50, Fire 50] split of raw 40 → 20 each; the enemy's
                // −1.0 FireResistance doubles only the Fire portion (20 → 40), Physical stays 20: 60/hit. The 180-HP
                // enemy (Str 26) dies on hit 3 at tick 30 → 1200ms. (No vulnerability — 40/hit — would kill on hit 5 /
                // 2000ms, so the per-portion vulnerability is decisive.)
                ["multiTypePerPortionVulnerability"] = new ParityScenario(
                    Player: () => MakeBattler(
                        strength: 10, endurance: 0,
                        skills: [MakeMultiTypeSkill(1, baseDamage: 40, cooldownMs: 400,
                            [(EDamageType.Physical, 50), (EDamageType.Fire, 50)])]),
                    Enemy: () => MakeEnemy(
                        strength: 26, endurance: 0, skills: [],
                        extra: [(EAttribute.FireResistance, -1.0)]),
                    ExpectedVictory: true,
                    ExpectedPlayerDied: false,
                    ExpectedTotalMs: 1200),

                // Per-portion absorption with the order-dependent heal cap. A [Physical 20, Fire 80] split of raw 100
                // against an enemy absorbing Fire (FireResistance 2.0) at full health: the Physical portion deals 20
                // (100 → 80, opening 20 room), then the Fire portion's −80 absorption heal is CAPPED at that 20 room
                // (back to 100). Net 20 + (−20) = 0/hit, so the enemy never loses health and never dies — a timeout
                // draw. The fixed Physical-first order is what lets the heal land; the cap binds (heal 80 vs room 20).
                ["multiTypePerPortionAbsorptionCap"] = new ParityScenario(
                    Player: () => MakeBattler(
                        strength: 0, endurance: 0,
                        skills: [MakeMultiTypeSkill(1, baseDamage: 100, cooldownMs: 400,
                            [(EDamageType.Physical, 20), (EDamageType.Fire, 80)])]),
                    Enemy: () => MakeEnemy(
                        strength: 10, endurance: 0, skills: [],
                        extra: [(EAttribute.FireResistance, 2.0)]),
                    ExpectedVictory: false,
                    ExpectedPlayerDied: false,
                    ExpectedTotalMs: GameConstants.DefaultMaxBattleMs),

                // Reduce-to-single-portion identity for a non-unit weight: raw × w ÷ w = raw exactly. A lone Fire
                // portion at weight 2 with no amp/resist deals 20/hit, so the 100-HP enemy dies on hit 5 → 2000ms —
                // identical to typedFireNoModifiersUnchanged, proving a single portion (any weight) is the single-type hit.
                ["singlePortionNonUnitWeightIdentity"] = new ParityScenario(
                    Player: () => MakeBattler(
                        strength: 10, endurance: 0,
                        skills: [MakeMultiTypeSkill(1, baseDamage: 20, cooldownMs: 400, [(EDamageType.Fire, 2.0)])]),
                    Enemy: () => MakeEnemy(strength: 10, endurance: 0, skills: []),
                    ExpectedVictory: true,
                    ExpectedPlayerDied: false,
                    ExpectedTotalMs: 2000),
            };

        /// <summary>A duration long enough that an effect never expires within a battle (for "permanent" buffs).</summary>
        private const int Permanent = 1_000_000;

        /// <summary>
        /// The shared battle-RNG seed both simulators construct their <see cref="Mulberry32"/> from. Its value
        /// is immaterial to the current scenarios (their crit/dodge/block chances are forced to 1/0, so the
        /// outcome never depends on a draw) but both suites must seed identically — the frontend mirror passes
        /// the same constant — so the threaded seed is exercised and a future real-probability scenario stays
        /// in lockstep.
        /// </summary>
        private const uint ParitySeed = 0x9E3779B9;

        public static IEnumerable<object[]> ScenarioNames =>
            Scenarios.Keys.Select(name => new object[] { name });

        [Theory]
        [MemberData(nameof(ScenarioNames))]
        public void Parity_Scenario_MatchesExpectedResult(string scenarioName)
        {
            var scenario = Scenarios[scenarioName];

            var sim = new BattleSimulator(scenario.Player(), scenario.Enemy(), ParitySeed);
            var result = sim.Simulate(scenario.MaxMs);

            Assert.Equal(scenario.ExpectedVictory, result.Victory);
            Assert.Equal(scenario.ExpectedPlayerDied, result.PlayerDied);
            Assert.Equal(scenario.ExpectedTotalMs, result.TotalMs);
            Assert.Equal(0, result.TotalMs % 40);
        }

        /// <summary>
        /// Demonstrates what happens if the player's derived stats are double-counted
        /// (the bug the frontend used to have). CooldownRecovery doubles from 1.09→2.18
        /// (the base-1 multiplier counted twice), so skills fire every 14 ticks instead of 28,
        /// ending the same matchup as the <c>cooldownRecovery</c> scenario (13440ms) much sooner.
        /// Kept separate from the matrix because it builds the player from already-final
        /// attribute values rather than raw allocations.
        /// </summary>
        [Fact]
        public void Parity_DoubleDerivedStats_ProducesShorterBattle()
        {
            var player = MakePlayerWithDoubleDerivedStats(
                strength: 50, endurance: 30, agility: 20, dexterity: 10,
                skills: [MakeSkill(1, baseDamage: 10, cooldownMs: 1200, mult: EAttribute.Strength, multAmount: 1.5)]);

            var enemy = MakeEnemy(
                strength: 10, endurance: 15,
                skills: [MakeSkill(2, baseDamage: 5, cooldownMs: 2000)]);

            var sim = new BattleSimulator(BattlerFactory.FromPlayer(player), enemy, ParitySeed);
            var result = sim.Simulate();

            Assert.True(result.Victory);
            // With doubled CDR (2.18 instead of 1.09), cdMult=2.18, charge/tick = 87.2, so the skill fires every
            // 14 ticks instead of 28 — 12 hits (34 each vs the enemy's Toughness 30) land in half the ticks → 6720ms.
            Assert.Equal(6720, result.TotalMs);
            Assert.True(result.TotalMs < 13440, "Double-counted stats should end the battle sooner.");
        }

        /// <summary>
        /// Pins down the merged attribute values for the <c>equippedItemWithMods</c> scenario so a
        /// divergence in the stat + item + applied-mod attribute composition is reported directly
        /// (rather than only surfacing as a different battle outcome). Mirrors the frontend
        /// "composes the equippedItemWithMods stat + item + mod attributes" expectation.
        /// </summary>
        [Fact]
        public void Parity_EquippedItemWithMods_ComposesMergedAttributes()
        {
            var player = Scenarios["equippedItemWithMods"].Player();

            // Strength 20 (allocation) + 10 (item) + 8 (prefix) + 7 (suffix) = 45.
            Assert.Equal(45, player.GetAttributeValue(EAttribute.Strength));
            Assert.Equal(20, player.GetAttributeValue(EAttribute.Endurance));
            Assert.Equal(20, player.GetAttributeValue(EAttribute.Agility));
            Assert.Equal(20, player.GetAttributeValue(EAttribute.Dexterity));
            Assert.Equal(675, player.GetAttributeValue(EAttribute.MaxHealth));
            Assert.Equal(40, player.GetAttributeValue(EAttribute.Toughness));
            Assert.Equal(1.10, player.GetAttributeValue(EAttribute.CooldownRecovery), 10);
            Assert.Equal(1.10, player.GetCooldownMultiplier(), 10);
        }

        /// <summary>
        /// Pins the fractional enemy attributes for the <c>fractionalEnemyDistribution</c> scenario so a
        /// divergence in the decimal→double per-level distribution (or the wire roundtrip the client mirrors)
        /// is reported directly rather than only surfacing as a different battle outcome. Mirrors the frontend
        /// "derives the fractionalEnemyDistribution enemy MaxHealth" expectation.
        /// </summary>
        [Fact]
        public void Parity_FractionalEnemyDistribution_DerivesFractionalMaxHealth()
        {
            var enemy = Scenarios["fractionalEnemyDistribution"].Enemy();

            // Strength 0 (base) + (0 + 2.5 × level 3) per-level distribution = 7.5 → MaxHealth 50 + 5×7.5 = 87.5.
            Assert.Equal(7.5, enemy.GetAttributeValue(EAttribute.Strength));
            Assert.Equal(87.5, enemy.GetAttributeValue(EAttribute.MaxHealth));
        }

        private static Skill MakeSkill(
            int id, double baseDamage, int cooldownMs,
            EAttribute? mult = null, double multAmount = 0,
            List<SkillEffect>? effects = null,
            EDamageType damageType = EDamageType.Physical) => new()
            {
                Id = id,
                Name = $"Skill {id}",
                Description = "",
                DamagePortions = [new SkillDamagePortion { Type = damageType, Weight = 1.0 }],
                CooldownMs = cooldownMs,
                BaseDamage = baseDamage,
                DamageMultipliers = mult is null
                    ? []
                    :
                    [
                        new DamageMultiplier
                        {
                            Attribute = mult.Value,
                            Amount = multAmount,
                        }
                    ],
                Effects = effects ?? [],
            };

        /// <summary>
        /// A skill carrying a multi-typed weighted damage-portion split (#1343). Weights are stored raw and
        /// normalized at fire time (<c>raw × weight ÷ Σweights</c>), so they need not sum to 1.
        /// </summary>
        private static Skill MakeMultiTypeSkill(
            int id, double baseDamage, int cooldownMs, (EDamageType Type, double Weight)[] portions) => new()
            {
                Id = id,
                Name = $"Skill {id}",
                Description = "",
                DamagePortions = portions.Select(p => new SkillDamagePortion { Type = p.Type, Weight = p.Weight }).ToList(),
                CooldownMs = cooldownMs,
                BaseDamage = baseDamage,
                DamageMultipliers = [],
                Effects = [],
            };

        private static SkillEffect MakeEffect(
            int id, ESkillEffectTarget target, EAttribute attribute,
            EModifierType modifierType, double amount, int durationMs,
            EAttribute scalingAttribute = EAttribute.Strength, double scalingAmount = 0) => new()
            {
                Id = id,
                Target = target,
                AttributeId = attribute,
                ModifierType = modifierType,
                Amount = amount,
                DurationMs = durationMs,
                ScalingAttributeId = scalingAttribute,
                ScalingAmount = scalingAmount,
            };

        private static Battler MakeBattler(
            double strength, double endurance, double agility = 0, double dexterity = 0,
            List<Skill>? skills = null,
            (EAttribute Attribute, double Amount)[]? extra = null)
        {
            return BattlerFactory.FromPlayer(MakePlayer(strength, endurance, agility, dexterity, skills, extra));
        }

        private static Player MakePlayer(
            double strength, double endurance, double agility = 0, double dexterity = 0,
            List<Skill>? skills = null,
            (EAttribute Attribute, double Amount)[]? extra = null)
        {
            var allocations = new List<StatAllocation>
            {
                new() { Attribute = EAttribute.Strength,  Amount = strength },
                new() { Attribute = EAttribute.Endurance, Amount = endurance },
                new() { Attribute = EAttribute.Intellect, Amount = 0 },
                new() { Attribute = EAttribute.Agility,   Amount = agility },
                new() { Attribute = EAttribute.Dexterity, Amount = dexterity },
                new() { Attribute = EAttribute.Luck,      Amount = 0 },
            };

            // Extra non-core attributes (e.g. forced crit/dodge chances, DamageReflection) ride in as additive
            // allocations layered on top of the static derivations that source them (#799) — so an injected
            // CriticalDamage amount here is a delta over its derived base (1.5), while authored-only attributes
            // like DamageReflection (no derivation) are the value itself.
            if (extra is not null)
            {
                foreach (var (attribute, amount) in extra)
                {
                    allocations.Add(new StatAllocation { Attribute = attribute, Amount = amount });
                }
            }

            var totalUsed = (int)(strength + endurance + agility + dexterity);
            var defaultSkills = skills ?? [];

            return new PlayerBuilder()
                .WithStatAllocations(allocations)
                .WithStatPointsGained(totalUsed)
                .WithStatPointsUsed(totalUsed)
                .WithSkills(defaultSkills)
                .WithSelectedSkills(defaultSkills)
                .Build();
        }

        /// <summary>
        /// Builds a player Battler whose attributes flow through the equipped-item + applied-mod
        /// composition path (<see cref="Item.GetAttributeModifiers"/> via the live
        /// <see cref="Player.GetAttributes"/>), layering the item's own attributes and every applied
        /// mod's attributes on top of the raw stat allocations. The item is given one mod slot per
        /// mod (index- and type-aligned) so each mod applies, mirroring how the frontend
        /// <c>makeEquipment</c> helper feeds the same merged attributes to its Battler.
        /// </summary>
        private static Battler MakeBattlerWithEquipment(
            double strength, double endurance,
            List<Skill> skills,
            List<AttributeModifier> itemAttributes,
            List<ItemMod> mods)
        {
            var item = new Item
            {
                Id = 1,
                Name = "Item 1",
                Description = string.Empty,
                Category = EItemCategory.Accessory,
                Rarity = ERarity.Common,
                Attributes = itemAttributes,
                ModSlots = mods.Select((mod, index) => new ItemModSlot
                {
                    Id = index,
                    Type = mod.Type,
                }).ToList(),
            };

            var player = MakePlayer(strength, endurance, skills: skills);
            player.UnlockItem(item);
            player.TryEquipItem(item.Id, EEquipmentSlot.AccessorySlot, new Dictionary<int, int>());
            for (var index = 0; index < mods.Count; index++)
            {
                player.UnlockMod(mods[index].Id);
                player.TryApplyMod(item.Id, mods[index].Id, index, mods[index]);
            }

            return BattlerFactory.FromPlayer(player);
        }

        /// <summary>
        /// Builds a player Battler through the snapshot path (<see cref="BattleSnapshot.ToBattler"/>, the
        /// production anti-cheat replay surface) whose battle skills are the <paramref name="selectedSkillIds"/>
        /// plus the skills the equipped items in <paramref name="grants"/> grant — each grant a
        /// (itemId, grantedSkillId) pair. Exercises the item-granted-skill append/order/dedupe end to end:
        /// every referenced skill (selected or granted) is resolved from <paramref name="skillPool"/>.
        /// </summary>
        private static Battler MakeBattlerWithGrants(
            double strength,
            List<int> selectedSkillIds,
            List<(int ItemId, int GrantedSkillId)> grants,
            List<Skill> skillPool)
        {
            var snapshot = new BattleSnapshot
            {
                Level = 1,
                StatAllocations = [new StatAllocation { Attribute = EAttribute.Strength, Amount = strength }],
                EquippedItems = grants
                    .Select(g => new EquippedItemSnapshot { ItemId = g.ItemId, AppliedModIds = [] })
                    .ToList(),
                SkillIds = selectedSkillIds,
            };

            var itemsById = grants.ToDictionary(g => g.ItemId, g => GrantItem(g.ItemId, g.GrantedSkillId));
            var skillsById = skillPool.ToDictionary(s => s.Id);

            return snapshot.ToBattler(
                id => itemsById[id],
                _ => throw new InvalidOperationException("No mods are applied in the granted-skill scenarios."),
                id => skillsById[id]);
        }

        private static Item GrantItem(int id, int grantedSkillId) => new()
        {
            Id = id,
            Name = $"Item {id}",
            Description = string.Empty,
            Category = EItemCategory.Accessory,
            Rarity = ERarity.Common,
            GrantedSkillId = grantedSkillId,
            Attributes = [],
            ModSlots = [],
        };

        private static ItemMod MakeMod(int id, EItemModType type, List<AttributeModifier> attributes) => new()
        {
            Id = id,
            Name = $"Mod {id}",
            Description = string.Empty,
            Type = type,
            Rarity = ERarity.Common,
            Attributes = attributes,
        };

        private static AttributeModifier ItemAttr(EAttribute attribute, double amount) => new()
        {
            Attribute = attribute,
            Amount = amount,
            Type = EModifierType.Additive,
            Source = EAttributeModifierSource.Item,
        };

        /// <summary>
        /// Creates a player whose attributes mimic the frontend double-counting bug:
        /// stat allocations include FINAL values (with derived stats baked in),
        /// and the AttributeCollection adds derived stats again on top.
        /// </summary>
        private static Player MakePlayerWithDoubleDerivedStats(
            double strength, double endurance, double agility = 0, double dexterity = 0,
            List<Skill>? skills = null)
        {
            // First, compute the correct final values (as the API would send them).
            double maxHealth = 50 + 20 * endurance + 5 * strength;
            double toughness = 2 * endurance;
            double cooldownRecovery = 1 + 0.004 * agility + 0.001 * dexterity;

            // These allocations include both raw stats AND derived values,
            // mimicking what PlayerData.FromPlayer sends to the frontend.
            var allocations = new List<StatAllocation>
            {
                new() { Attribute = EAttribute.Strength,        Amount = strength },
                new() { Attribute = EAttribute.Endurance,       Amount = endurance },
                new() { Attribute = EAttribute.Intellect,       Amount = 0 },
                new() { Attribute = EAttribute.Agility,         Amount = agility },
                new() { Attribute = EAttribute.Dexterity,       Amount = dexterity },
                new() { Attribute = EAttribute.Luck,            Amount = 0 },
                new() { Attribute = EAttribute.MaxHealth,       Amount = maxHealth },
                new() { Attribute = EAttribute.Toughness,       Amount = toughness },
                new() { Attribute = EAttribute.CooldownRecovery,Amount = cooldownRecovery },
            };

            var totalUsed = (int)(strength + endurance + agility + dexterity);
            var defaultSkills = skills ?? [];

            return new PlayerBuilder()
                .WithStatAllocations(allocations)
                .WithStatPointsGained(totalUsed)
                .WithStatPointsUsed(totalUsed)
                .WithSkills(defaultSkills)
                .WithSelectedSkills(defaultSkills)
                .Build();
        }

        private static Battler MakeEnemy(
            double strength, double endurance,
            List<Skill>? skills = null,
            (EAttribute Attribute, double Amount)[]? extra = null,
            int level = 1,
            (EAttribute Attribute, decimal BaseAmount, decimal AmountPerLevel)[]? perLevelDistributions = null)
        {
            var defaultSkills = skills ?? [];

            var distributions = new List<AttributeDistribution>
            {
                new() { AttributeId = EAttribute.Strength, BaseAmount = (decimal)strength, AmountPerLevel = 0 },
                new() { AttributeId = EAttribute.Endurance, BaseAmount = (decimal)endurance, AmountPerLevel = 0 },
            };

            // Forced crit/dodge chances used by the "enemies never crit/dodge" scenario — they must be present
            // on the enemy yet never consulted, proving the rolls are gated on the player alone.
            if (extra is not null)
            {
                foreach (var (attribute, amount) in extra)
                {
                    distributions.Add(new AttributeDistribution
                    {
                        AttributeId = attribute,
                        BaseAmount = (decimal)amount,
                        AmountPerLevel = 0,
                    });
                }
            }

            // Fractional per-level distributions (#941): exercise the AmountPerLevel × level path of
            // AttributeDistribution.GetDistributionModifier — every other scenario uses an integer BaseAmount
            // at level 1, so this is the only place the decimal→double per-level derivation is covered.
            if (perLevelDistributions is not null)
            {
                foreach (var (attribute, baseAmount, amountPerLevel) in perLevelDistributions)
                {
                    distributions.Add(new AttributeDistribution
                    {
                        AttributeId = attribute,
                        BaseAmount = baseAmount,
                        AmountPerLevel = amountPerLevel,
                    });
                }
            }

            var enemy = new Enemy
            {
                Id = 1,
                Name = "Test Enemy",
                IsBoss = false,
                Level = level,
                AttributeDistributions = distributions,
                AvailableSkills = defaultSkills,
            };
            enemy.SetBattleSkills(defaultSkills.Select(s => s.Id).ToList());
            return new Battler(new AttributeCollection(enemy.GetAttributeModifiers()), enemy.BattleSkills, enemy.Level);
        }
    }
}
