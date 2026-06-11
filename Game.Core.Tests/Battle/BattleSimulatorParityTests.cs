using Game.Core.Attributes;
using Game.Core.Attributes.Modifiers;
using Game.Core.Battle;
using Game.Core.Enemies;
using Game.Core.Items;
using Game.Core.Players;
using Game.Core.Players.Inventories;
using Game.Core.Skills;
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
                //   Player: MaxHealth=900, Def=42, CDR=9 → cdMult=1.09
                //     charge/tick = 40*1.09 = 43.6, fires every 28 ticks (28*43.6=1220.8≥1200)
                //     damage = 10 + 50*1.5 = 85, after def = 85-17 = 68
                //   Enemy:  MaxHealth=400, Def=17, CDR=0; damage = 5-42 = 0 (clamped)
                //   6 hits to kill (6*68=408>400), at ticks 28,56,84,112,140,168 → 6720ms
                ["cooldownRecovery"] = new ParityScenario(
                    Player: () => MakeBattler(
                        strength: 50, endurance: 30, agility: 20, dexterity: 10,
                        skills: [MakeSkill(1, baseDamage: 10, cooldownMs: 1200, mult: EAttribute.Strength, multAmount: 1.5)]),
                    Enemy: () => MakeEnemy(
                        strength: 10, endurance: 15,
                        skills: [MakeSkill(2, baseDamage: 5, cooldownMs: 2000)]),
                    ExpectedVictory: true,
                    ExpectedPlayerDied: false,
                    ExpectedTotalMs: 6720),

                // Two player skills firing on different cooldowns against an enemy
                // that deals real (non-clamped) damage — a genuine multi-skill exchange.
                //   Player: MaxHealth=600, Def=22, cdMult=1
                //     skillA: 20 + 30*1.0 = 50 raw, after def = 33, fires every 20 ticks
                //     skillB: 25 raw, after def = 8, fires every 30 ticks
                //   Enemy:  MaxHealth=450, Def=17; attack 30 raw, after def = 8, every 25 ticks
                //   Cumulative player damage first reaches 450 at tick 240 → 9600ms.
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
                    ExpectedTotalMs: 9600),

                // Both combatants have so much Defense that every hit clamps to 0,
                // so neither can ever win — the battle runs to the timeout.
                //   Player/Enemy: Def=52 each; attacks 5 raw → 5-52 clamped to 0.
                ["highDefenseFloor"] = new ParityScenario(
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
                //   Player: Str=60, End=40 → MaxHealth=1150, Def=42, cdMult=1.
                //     skill: 10 + 60*2.0 = 130 raw, after boss def 62 → 68, fires every 25 ticks.
                //   Boss:   Str=20, End=60 → MaxHealth=1350, Def=62, cdMult=1.
                //     3 skills (50/20/30 raw): only the 50 pierces the player's def (8 dmg), the rest clamp to 0.
                //   20 player hits reach 1360 ≥ 1350 at tick 500 → 20000ms; the boss never threatens the player.
                ["bossFullLoadout"] = new ParityScenario(
                    Player: () => MakeBattler(
                        strength: 60, endurance: 40,
                        skills: [MakeSkill(1, baseDamage: 10, cooldownMs: 1000, mult: EAttribute.Strength, multAmount: 2.0)]),
                    Enemy: () => MakeEnemy(
                        strength: 20, endurance: 60,
                        skills:
                        [
                            MakeSkill(2, baseDamage: 50, cooldownMs: 1000),
                            MakeSkill(3, baseDamage: 20, cooldownMs: 1000),
                            MakeSkill(4, baseDamage: 30, cooldownMs: 1000),
                        ]),
                    ExpectedVictory: true,
                    ExpectedPlayerDied: false,
                    ExpectedTotalMs: 20000),

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
                //   Merged: Str=45, End=20, Agi=20, Dex=20 → MaxHealth=675, Def=32, CDR=10 → cdMult=1.10.
                //   Player skill: 10 + 45*1.5 = 77.5 raw, after enemy def 17 → 60.5, fires every 28 ticks
                //     (charge/tick = 40*1.10 = 44, 44*28=1232 ≥ 1200).
                //   Enemy: MaxHealth=400, Def=17; attack 5-32 clamps to 0, so the player never dies.
                //   7 hits reach 423.5 ≥ 400 at tick 196 → 7840ms (vs 21600ms for the same allocations
                //   with no equipment — proof the merged attributes change the outcome).
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
                    ExpectedTotalMs: 7840),

                // A self Strength buff: the hit that CARRIES the effect uses the pre-effect attributes,
                // and only subsequent hits see the boost (which also raises damage via the Strength→damage
                // path) — and re-applying it every fire REFRESHES rather than stacks (Str stays 20, not 30+).
                //   Player: Str=10, cdMult=1; skill = Str×1.0 raw, fires every 10 ticks (cooldown 400).
                //     Effect: Self +10 Strength (additive), effectively permanent.
                //   Enemy:  Str=10 → MaxHealth=100, Def=2, no skills.
                //   Hit 1 deals Str(10)−2 = 8 (pre-buff); hits 2+ deal Str(20)−2 = 18.
                //   Enemy HP 100 → 92,74,56,38,20,2,−16: dies on hit 7 at tick 2800.
                //   (A buggy carrying-hit boost would kill on hit 6 = 2400; stacking would kill even sooner.)
                ["selfStrengthBuffAffectsLaterHits"] = new ParityScenario(
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
                    ExpectedTotalMs: 2800),

                // A self CooldownRecovery buff is read live each tick, so it shortens the fire interval after
                // the first hit applies it — proving CDR buffs change fire ticks.
                //   Player: Str=20, base CDR=0 (cdMult=1); skill = Str×1.0 raw, cooldown 400.
                //     Effect: Self +100 CooldownRecovery (additive) → cdMult=2 once applied, effectively permanent.
                //   Enemy:  Str=10 → MaxHealth=100, Def=2, no skills. Each hit deals 20−2 = 18.
                //   Hit 1 fires at 400 (cdMult=1) and applies the buff; thereafter charge/tick = 80, so the
                //   skill fires every 200ms: hits at 400,600,800,1000,1200,1400 → enemy dies on hit 6 at 1400.
                //   (Ignoring the live CDR read would keep the 400ms interval → hit 6 at 2400.)
                ["cdrBuffShortensFireInterval"] = new ParityScenario(
                    Player: () => MakeBattler(
                        strength: 20, endurance: 0,
                        skills:
                        [
                            MakeSkill(1, baseDamage: 0, cooldownMs: 400, mult: EAttribute.Strength, multAmount: 1.0,
                                effects: [MakeEffect(101, ESkillEffectTarget.Self, EAttribute.CooldownRecovery, EModifierType.Additive, 100, Permanent)]),
                        ]),
                    Enemy: () => MakeEnemy(
                        strength: 10, endurance: 0,
                        skills: []),
                    ExpectedVictory: true,
                    ExpectedPlayerDied: false,
                    ExpectedTotalMs: 1400),

                // Same-tick ordering: an earlier loadout slot's self buff influences a later slot firing on the
                // same tick. Slot 0 buffs Strength after dealing its (pre-buff) damage, then slot 1 — firing
                // after it on the same tick — deals its damage from the already-buffed Strength.
                //   Player: Str=10; slot0 = Str×1.0 (Self +10 Str, permanent), slot1 = Str×1.0 (no effect),
                //     both cooldown 400 so they fire together at 400,800,…; cdMult=1.
                //   Enemy:  Str=16 → MaxHealth=130, Def=2, no skills.
                //   Tick 400: slot0 deals 10−2=8 then buffs Str→20; slot1 deals 20−2=18 → 26 that tick.
                //   Ticks 800+: both deal 18 → 36/tick. Enemy HP 130 → 104,68,32,−4: dies at tick 1600.
                //   (If slot1 didn't see slot0's buff, tick 400 would deal only 16 and the kill would slip to 2000.)
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
                    ExpectedTotalMs: 1600),

                // MaxHealth clamp: an Opponent MaxHealth ×0.5 debuff halves the enemy's maximum, and because its
                // current health exceeds the new max it is clamped down immediately — bringing the kill forward.
                //   Player: skill baseDamage 12, no multiplier → 12−2 = 10 per hit, cooldown 400.
                //     Effect: Opponent ×0.5 MaxHealth (multiplicative), effectively permanent.
                //   Enemy:  Str=10 → MaxHealth=100, Def=2, no skills.
                //   Tick 400: deals 10 (100→90) then halves MaxHealth to 50 → clamps 90 down to 50.
                //   Then 50→40→30→20→10→0 over the next hits: dies at tick 2400 (vs 4000 with no debuff).
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
                    ExpectedTotalMs: 2400),

                // Same-tick CDR ordering: slot 0's self CooldownRecovery buff (applied after it fires) speeds
                // up slot 1's charge accrual ON THE SAME TICK, because each slot reads the cooldown multiplier
                // live in loadout order — so an earlier slot's effect influences a later slot firing that tick.
                //   Player: base CDR=0 (cdMult=1). slot0 = pure buffer (0 damage, cooldown 40, Self +100 CDR
                //     permanent → cdMult=2 once applied); slot1 = baseDamage 27, cooldown 400, no multiplier.
                //   Enemy:  Str=5 → MaxHealth=75, Def=2, no skills. Each slot1 hit deals 27−2 = 25.
                //   The buff lifts slot1's accrual to 80/tick from the first tick, so slot1 fires every 200ms at
                //   ticks 200,400,600 → enemy dies on the 3rd hit at 600. (Accruing every slot before any effect
                //   — a two-phase order — would slip slot1's first fire to 240 and the kill to 640.)
                ["cdrBuffSpeedsLaterSlotSameTick"] = new ParityScenario(
                    Player: () => MakeBattler(
                        strength: 0, endurance: 0,
                        skills:
                        [
                            MakeSkill(1, baseDamage: 0, cooldownMs: 40,
                                effects: [MakeEffect(104, ESkillEffectTarget.Self, EAttribute.CooldownRecovery, EModifierType.Additive, 100, Permanent)]),
                            MakeSkill(2, baseDamage: 27, cooldownMs: 400),
                        ]),
                    Enemy: () => MakeEnemy(
                        strength: 5, endurance: 0,
                        skills: []),
                    ExpectedVictory: true,
                    ExpectedPlayerDied: false,
                    ExpectedTotalMs: 600),
            };

        /// <summary>A duration long enough that an effect never expires within a battle (for "permanent" buffs).</summary>
        private const int Permanent = 1_000_000;

        public static IEnumerable<object[]> ScenarioNames =>
            Scenarios.Keys.Select(name => new object[] { name });

        [Theory]
        [MemberData(nameof(ScenarioNames))]
        public void Parity_Scenario_MatchesExpectedResult(string scenarioName)
        {
            var scenario = Scenarios[scenarioName];

            var sim = new BattleSimulator(scenario.Player(), scenario.Enemy());
            var result = sim.Simulate(scenario.MaxMs);

            Assert.Equal(scenario.ExpectedVictory, result.Victory);
            Assert.Equal(scenario.ExpectedPlayerDied, result.PlayerDied);
            Assert.Equal(scenario.ExpectedTotalMs, result.TotalMs);
            Assert.Equal(0, result.TotalMs % 40);
        }

        /// <summary>
        /// Demonstrates what happens if the player's derived stats are double-counted
        /// (the bug the frontend used to have). CooldownRecovery doubles from 9→18,
        /// cdMultiplier goes from 1.09→1.18, skills fire every 26 ticks instead of 28,
        /// so the same matchup as the <c>cooldownRecovery</c> scenario ends sooner.
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

            var sim = new BattleSimulator(new Battler(player), enemy);
            var result = sim.Simulate();

            Assert.True(result.Victory);
            // With doubled CDR (18 instead of 9), cdMult=1.18,
            // charge/tick = 47.2, fires every 26 ticks → 6240ms
            Assert.Equal(6240, result.TotalMs);
            Assert.True(result.TotalMs < 6720, "Double-counted stats should end the battle sooner.");
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
            Assert.Equal(32, player.GetAttributeValue(EAttribute.Defense));
            Assert.Equal(10, player.GetAttributeValue(EAttribute.CooldownRecovery));
            Assert.Equal(1.10, player.GetCooldownMultiplier(), 10);
        }

        private static Skill MakeSkill(
            int id, double baseDamage, int cooldownMs,
            EAttribute? mult = null, double multAmount = 0,
            List<SkillEffect>? effects = null) => new()
            {
                Id = id,
                Name = $"Skill {id}",
                Description = "",
                CooldownMs = cooldownMs,
                BaseDamage = baseDamage,
                DamageMultipliers = mult is null
                    ? []
                    :
                    [
                        new AttributeModifier
                        {
                            Attribute = mult.Value,
                            Amount = multAmount,
                            Type = EModifierType.Multiplicative,
                            Source = EAttributeModifierSource.Derived,
                        }
                    ],
                Effects = effects ?? [],
            };

        private static SkillEffect MakeEffect(
            int id, ESkillEffectTarget target, EAttribute attribute,
            EModifierType modifierType, double amount, int durationMs) => new()
            {
                Id = id,
                Target = target,
                AttributeId = attribute,
                ModifierType = modifierType,
                Amount = amount,
                DurationMs = durationMs,
            };

        private static Battler MakeBattler(
            double strength, double endurance, double agility = 0, double dexterity = 0,
            List<Skill>? skills = null)
        {
            return new Battler(MakePlayer(strength, endurance, agility, dexterity, skills));
        }

        private static Player MakePlayer(
            double strength, double endurance, double agility = 0, double dexterity = 0,
            List<Skill>? skills = null)
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

            var totalUsed = (int)(strength + endurance + agility + dexterity);
            var defaultSkills = skills ?? [];

            return new Player
            {
                Id = 1,
                Name = "Test",
                Level = 1,
                Exp = 0,
                CurrentZoneId = 0,
                StatPoints = new PlayerStatPoints(allocations)
                { StatPointsGained = totalUsed, StatPointsUsed = totalUsed },
                Inventory = new Inventory(),
                SelectedSkills = defaultSkills,
                Skills = defaultSkills,
                LogPreferences = [],
            };
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
                Tags = [],
            };

            var player = MakePlayer(strength, endurance, skills: skills);
            player.UnlockItem(item);
            player.TryEquipItem(item.Id, EEquipmentSlot.AccessorySlot);
            for (var index = 0; index < mods.Count; index++)
            {
                player.UnlockMod(mods[index].Id);
                player.TryApplyMod(item.Id, mods[index].Id, index, mods[index]);
            }

            return new Battler(player);
        }

        private static ItemMod MakeMod(int id, EItemModType type, List<AttributeModifier> attributes) => new()
        {
            Id = id,
            Name = $"Mod {id}",
            Description = string.Empty,
            Type = type,
            Rarity = ERarity.Common,
            Attributes = attributes,
            Tags = [],
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
            double defense = 2 + endurance + 0.5 * agility;
            double cooldownRecovery = 0.4 * agility + 0.1 * dexterity;

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
                new() { Attribute = EAttribute.Defense,         Amount = defense },
                new() { Attribute = EAttribute.CooldownRecovery,Amount = cooldownRecovery },
            };

            var totalUsed = (int)(strength + endurance + agility + dexterity);
            var defaultSkills = skills ?? [];

            return new Player
            {
                Id = 1,
                Name = "Test",
                Level = 1,
                Exp = 0,
                CurrentZoneId = 0,
                StatPoints = new PlayerStatPoints(allocations)
                { StatPointsGained = totalUsed, StatPointsUsed = totalUsed },
                Inventory = new Inventory(),
                SelectedSkills = defaultSkills,
                Skills = defaultSkills,
                LogPreferences = [],
            };
        }

        private static Battler MakeEnemy(
            double strength, double endurance,
            List<Skill>? skills = null)
        {
            var defaultSkills = skills ?? [];

            var enemy = new Enemy
            {
                Id = 1,
                Name = "Test Enemy",
                IsBoss = false,
                Level = 1,
                AttributeDistributions =
                [
                    new AttributeDistribution
                    {
                        AttributeId = EAttribute.Strength,
                        BaseAmount = (decimal)strength,
                        AmountPerLevel = 0,
                    },
                    new AttributeDistribution
                    {
                        AttributeId = EAttribute.Endurance,
                        BaseAmount = (decimal)endurance,
                        AmountPerLevel = 0,
                    },
                ],
                AvailableSkills = defaultSkills,
            };
            enemy.SetBattleSkills(defaultSkills.Select(s => s.Id).ToList());
            return new Battler(new AttributeCollection(enemy.GetAttributeModifiers()), enemy.BattleSkills, enemy.Level);
        }
    }
}
