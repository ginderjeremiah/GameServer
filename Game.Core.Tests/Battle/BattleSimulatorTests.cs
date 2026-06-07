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
    public class BattleSimulatorTests
    {
        [Fact]
        public void Simulate_StrongerPlayer_ReturnsVictory()
        {
            var player = MakePlayer(strength: 100, endurance: 100);
            var enemy = MakeEnemy(statTotal: 10);

            var sim = new BattleSimulator(new Battler(player), enemy);
            var result = sim.Simulate();

            Assert.True(result.Victory);
        }

        [Fact]
        public void Simulate_WeakerPlayer_ReturnsDefeat()
        {
            var player = MakePlayer(strength: 1, endurance: 1);
            var enemy = MakeEnemy(statTotal: 200);

            var sim = new BattleSimulator(new Battler(player), enemy);
            var result = sim.Simulate();

            Assert.False(result.Victory);
        }

        [Fact]
        public void Simulate_Victory_TotalMsIsPositive()
        {
            var player = MakePlayer(strength: 100, endurance: 100);
            var enemy = MakeEnemy(statTotal: 10);

            var sim = new BattleSimulator(new Battler(player), enemy);
            var result = sim.Simulate();

            Assert.True(result.TotalMs > 0);
        }

        [Fact]
        public void Simulate_NoSkillsOnEitherSide_TimesOut()
        {
            var player = MakePlayer(strength: 10, endurance: 10, skills: []);
            var enemy = MakeEnemy(statTotal: 10, skills: []);

            var sim = new BattleSimulator(new Battler(player), enemy);
            var result = sim.Simulate();

            Assert.False(result.Victory);
            Assert.False(result.PlayerDied);
            Assert.Equal(40 * 10000, result.TotalMs);
        }

        [Fact]
        public void Simulate_TotalMs_IsMultipleOfTickRate()
        {
            var player = MakePlayer(strength: 50, endurance: 50);
            var enemy = MakeEnemy(statTotal: 20);

            var sim = new BattleSimulator(new Battler(player), enemy);
            var result = sim.Simulate();

            Assert.Equal(0, result.TotalMs % 40);
        }

        [Fact]
        public void Simulate_Victory_CollectsStats()
        {
            var player = MakePlayer(strength: 100, endurance: 100);
            var enemy = MakeEnemy(statTotal: 10);

            var sim = new BattleSimulator(new Battler(player), enemy);
            var result = sim.Simulate();

            Assert.True(result.Victory);
            Assert.True(result.Stats.PlayerDamageDealt > 0);
            Assert.True(result.Stats.HighestPlayerAttack > 0);
            Assert.True(result.Stats.PlayerSkillsUsed > 0);
        }

        [Fact]
        public void Simulate_WithMaxMs_CapsSimulation()
        {
            var player = MakePlayer(strength: 50, endurance: 50);
            var enemy = MakeEnemy(statTotal: 50);

            var sim = new BattleSimulator(new Battler(player), enemy);
            var result = sim.Simulate(maxMs: 200);

            Assert.True(result.TotalMs <= 200);
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

        private static Battler MakeEnemy(double statTotal, List<Skill>? skills = null)
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
                        BaseAmount = (decimal)(statTotal / 2),
                        AmountPerLevel = 0,
                    },
                    new AttributeDistribution
                    {
                        AttributeId = EAttribute.Endurance,
                        BaseAmount = (decimal)(statTotal / 2),
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
