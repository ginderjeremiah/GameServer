using Game.Core.Attributes.Modifiers;
using Game.Core.Battle;
using Game.Core.Enemies;
using Game.Core.Players;
using Game.Core.Players.Inventories;
using Game.Core.Skills;

namespace Game.Core.Tests.Battle
{
    [TestClass]
    public class BattleSimulatorTests
    {
        [TestMethod]
        public void Simulate_StrongerPlayer_ReturnsVictory()
        {
            var player = MakePlayer(strength: 100, endurance: 100);
            var enemy = MakeEnemy(statTotal: 10);

            var sim = new BattleSimulator(new Battler(player), new Battler(enemy));
            var result = sim.Simulate();

            Assert.IsTrue(result.Victory);
        }

        [TestMethod]
        public void Simulate_WeakerPlayer_ReturnsDefeat()
        {
            var player = MakePlayer(strength: 1, endurance: 1);
            var enemy = MakeEnemy(statTotal: 200);

            var sim = new BattleSimulator(new Battler(player), new Battler(enemy));
            var result = sim.Simulate();

            Assert.IsFalse(result.Victory);
        }

        [TestMethod]
        public void Simulate_Victory_TotalMsIsPositive()
        {
            var player = MakePlayer(strength: 100, endurance: 100);
            var enemy = MakeEnemy(statTotal: 10);

            var sim = new BattleSimulator(new Battler(player), new Battler(enemy));
            var result = sim.Simulate();

            Assert.IsTrue(result.TotalMs > 0);
        }

        [TestMethod]
        public void Simulate_NoSkillsOnEitherSide_TimesOut()
        {
            var player = MakePlayer(strength: 10, endurance: 10, skills: []);
            var enemy = MakeEnemy(statTotal: 10, skills: []);

            var sim = new BattleSimulator(new Battler(player), new Battler(enemy));
            var result = sim.Simulate();

            Assert.IsFalse(result.Victory);
            Assert.IsFalse(result.PlayerDied);
            Assert.AreEqual(40 * 10000, result.TotalMs);
        }

        [TestMethod]
        public void Simulate_TotalMs_IsMultipleOfTickRate()
        {
            var player = MakePlayer(strength: 50, endurance: 50);
            var enemy = MakeEnemy(statTotal: 20);

            var sim = new BattleSimulator(new Battler(player), new Battler(enemy));
            var result = sim.Simulate();

            Assert.AreEqual(0, result.TotalMs % 40);
        }

        [TestMethod]
        public void Simulate_Victory_CollectsStats()
        {
            var player = MakePlayer(strength: 100, endurance: 100);
            var enemy = MakeEnemy(statTotal: 10);

            var sim = new BattleSimulator(new Battler(player), new Battler(enemy));
            var result = sim.Simulate();

            Assert.IsTrue(result.Victory);
            Assert.IsTrue(result.Stats.PlayerDamageDealt > 0);
            Assert.IsTrue(result.Stats.HighestPlayerAttack > 0);
            Assert.IsTrue(result.Stats.PlayerSkillsUsed > 0);
        }

        [TestMethod]
        public void Simulate_WithMaxMs_CapsSimulation()
        {
            var player = MakePlayer(strength: 50, endurance: 50);
            var enemy = MakeEnemy(statTotal: 50);

            var sim = new BattleSimulator(new Battler(player), new Battler(enemy));
            var result = sim.Simulate(maxMs: 200);

            Assert.IsTrue(result.TotalMs <= 200);
        }

        private static Player MakePlayer(double strength, double endurance, List<Skill>? skills = null)
        {
            var allocations = new List<StatAllocation>
            {
                new() { Attribute = EAttribute.Strength,  Amount = strength },
                new() { Attribute = EAttribute.Endurance, Amount = endurance },
                new() { Attribute = EAttribute.Intellect, Amount = 0 },
                new() { Attribute = EAttribute.Agility,   Amount = 0 },
                new() { Attribute = EAttribute.Dexterity, Amount = 0 },
                new() { Attribute = EAttribute.Luck,      Amount = 0 },
            };

            var totalUsed = (int)(strength + endurance);
            var defaultSkills = skills ?? [
                new Skill
                {
                    Id = 0,
                    Name = "Attack",
                    Description = "",
                    CooldownMs = 1000,
                    BaseDamage = 10,
                    DamageMultipliers = [
                        new AttributeModifier
                        {
                            Attribute = EAttribute.Strength,
                            Amount = 1.0,
                            Type = EModifierType.Multiplicative,
                            Source = EAttributeModifierSource.Derived,
                        }
                    ],
                }
            ];

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

        private static Enemy MakeEnemy(double statTotal, List<Skill>? skills = null)
        {
            var defaultSkills = skills ?? [
                new Skill
                {
                    Id = 0,
                    Name = "Scratch",
                    Description = "",
                    CooldownMs = 1500,
                    BaseDamage = 5,
                    DamageMultipliers = [],
                }
            ];

            return new Enemy
            {
                Id = 1,
                Name = "Test Enemy",
                Level = 1,
                AttributeDistributions =
                [
                    new Core.Attributes.AttributeDistribution
                    {
                        AttributeId = EAttribute.Strength,
                        BaseAmount = (decimal)(statTotal / 2),
                        AmountPerLevel = 0,
                    },
                    new Core.Attributes.AttributeDistribution
                    {
                        AttributeId = EAttribute.Endurance,
                        BaseAmount = (decimal)(statTotal / 2),
                        AmountPerLevel = 0,
                    },
                ],
                Skills = defaultSkills,
            };
        }
    }
}
