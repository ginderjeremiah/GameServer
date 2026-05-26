using Game.Core.Attributes;
using Game.Core.Attributes.Modifiers;
using Game.Core.Battle;
using Game.Core.Enemies;
using Game.Core.Players;
using Game.Core.Players.Inventories;
using Game.Core.Skills;

namespace Game.Core.Tests.Battle
{
    /// <summary>
    /// Tests that produce deterministic totalMs values for cross-checking
    /// against the frontend battle simulation.
    /// </summary>
    [TestClass]
    public class BattleSimulatorParityTests
    {
        /// <summary>
        /// Scenario with CooldownRecovery > 0 to exercise the cdMultiplier path.
        /// Frontend must produce the same totalMs when using RAW stat allocations
        /// (not pre-computed final attribute values from the API).
        /// </summary>
        [TestMethod]
        public void Parity_WithCooldownRecovery_MatchesExpectedTotalMs()
        {
            var playerSkill = new Skill
            {
                Id = 1,
                Name = "Slash",
                Description = "",
                CooldownMs = 1200,
                BaseDamage = 10,
                DamageMultipliers =
                [
                    new AttributeModifier
                    {
                        Attribute = EAttribute.Strength,
                        Amount = 1.5,
                        Type = EModifierType.Multiplicative,
                        Source = EAttributeModifierSource.Derived,
                    }
                ],
            };

            var enemySkill = new Skill
            {
                Id = 2,
                Name = "Bite",
                Description = "",
                CooldownMs = 2000,
                BaseDamage = 5,
                DamageMultipliers = [],
            };

            var player = MakePlayer(
                strength: 50, endurance: 30, agility: 20, dexterity: 10,
                skills: [playerSkill]);

            var enemy = MakeEnemy(
                strength: 10, endurance: 15,
                skills: [enemySkill]);

            var sim = new BattleSimulator(new Battler(player), new Battler(enemy));
            var result = sim.Simulate();

            Assert.IsTrue(result.Victory, "Player should win this matchup.");
            Assert.AreEqual(0, result.TotalMs % 40, "totalMs must be a multiple of the tick rate.");

            // Record the authoritative value — the frontend test must match this exactly.
            // Player: MaxHealth=900, Def=42, CDR=9 → cdMult=1.09
            //   charge/tick = 40*1.09 = 43.6, fires every 28 ticks (28*43.6=1220.8≥1200)
            //   damage = 10 + 50*1.5 = 85, after def = 85-17 = 68
            // Enemy:  MaxHealth=400, Def=17, CDR=0
            //   damage = 5-42 = 0 (clamped)
            // 6 hits to kill (6*68=408>400), at ticks 28,56,84,112,140,168 → 6720ms
            Assert.AreEqual(6720, result.TotalMs);
        }

        /// <summary>
        /// Demonstrates what happens if the player's derived stats are doubled
        /// (the bug the frontend currently has). CooldownRecovery doubles from 9→18,
        /// cdMultiplier goes from 1.09→1.18, skills fire every 26 ticks instead of 28.
        /// </summary>
        [TestMethod]
        public void Parity_DoubleDerivedStats_ProducesShorterBattle()
        {
            var playerSkill = new Skill
            {
                Id = 1,
                Name = "Slash",
                Description = "",
                CooldownMs = 1200,
                BaseDamage = 10,
                DamageMultipliers =
                [
                    new AttributeModifier
                    {
                        Attribute = EAttribute.Strength,
                        Amount = 1.5,
                        Type = EModifierType.Multiplicative,
                        Source = EAttributeModifierSource.Derived,
                    }
                ],
            };

            var enemySkill = new Skill
            {
                Id = 2,
                Name = "Bite",
                Description = "",
                CooldownMs = 2000,
                BaseDamage = 5,
                DamageMultipliers = [],
            };

            // Simulate what the frontend does: start with FINAL attribute values
            // (which already include derived stats), then add derived stats AGAIN.
            var player = MakePlayerWithDoubleDerivedStats(
                strength: 50, endurance: 30, agility: 20, dexterity: 10,
                skills: [playerSkill]);

            var enemy = MakeEnemy(
                strength: 10, endurance: 15,
                skills: [enemySkill]);

            var sim = new BattleSimulator(new Battler(player), new Battler(enemy));
            var result = sim.Simulate();

            Assert.IsTrue(result.Victory);
            // With doubled CDR (18 instead of 9), cdMult=1.18,
            // charge/tick = 47.2, fires every 26 ticks → 6240ms
            Assert.AreEqual(6240, result.TotalMs);
            Assert.IsTrue(result.TotalMs < 6720, "Double-counted stats should end the battle sooner.");
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

        private static Enemy MakeEnemy(
            double strength, double endurance,
            List<Skill>? skills = null)
        {
            var defaultSkills = skills ?? [];

            return new Enemy
            {
                Id = 1,
                Name = "Test Enemy",
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
                Skills = defaultSkills,
            };
        }
    }
}
