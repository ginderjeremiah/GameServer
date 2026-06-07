using Game.Core.Attributes;
using Game.Core.Attributes.Modifiers;
using Game.Core.Battle;
using Game.Core.Enemies;
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
    /// <c>UI/new-svelte/src/tests/lib/battle/battle-simulation-parity.test.ts</c>.
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
                    ExpectedTotalMs: 40 * 10000),

                // Neither side has any skills, so no damage is ever dealt and the
                // battle runs to the default timeout.
                ["noSkills"] = new ParityScenario(
                    Player: () => MakeBattler(strength: 10, endurance: 10, skills: []),
                    Enemy: () => MakeEnemy(strength: 10, endurance: 10, skills: []),
                    ExpectedVictory: false,
                    ExpectedPlayerDied: false,
                    ExpectedTotalMs: 40 * 10000),

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
            };

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

        private static Skill MakeSkill(
            int id, double baseDamage, int cooldownMs,
            EAttribute? mult = null, double multAmount = 0) => new()
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
