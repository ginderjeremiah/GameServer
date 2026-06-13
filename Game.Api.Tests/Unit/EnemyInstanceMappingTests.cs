using Game.Application.Services;
using Game.Core;
using Game.Core.Attributes;
using Game.Core.Enemies;
using Game.Core.Skills;
using Xunit;
using EnemyInstanceModel = Game.Api.Models.Enemies.EnemyInstance;

namespace Game.Api.Tests.Unit
{
    public class EnemyInstanceMappingTests
    {
        [Fact]
        public void FromSource_ProjectsBattleStartResultOntoEnemyInstance()
        {
            var enemy = MakeEnemy();
            enemy.SetBattleSkills([20, 10]);
            var result = new BattleStartResult { Enemy = enemy, Seed = 12345u };

            var model = EnemyInstanceModel.FromSource(result);

            Assert.Equal(7, model.Id);
            Assert.Equal(3, model.Level);
            Assert.Equal(12345u, model.Seed);
            // Loadout order is preserved exactly as the enemy's battle skills.
            Assert.Equal([20, 10], model.SelectedSkills);
        }

        [Fact]
        public void FromSource_ProjectsAttributeModifiersWithDecimalCast()
        {
            // BaseAmount 5 at level 3 => 5 + (2 * 3) = 11; the projection must (decimal)-cast the amount.
            var enemy = MakeEnemy();
            enemy.SetBattleSkills([]);
            var result = new BattleStartResult { Enemy = enemy, Seed = 1u };

            var model = EnemyInstanceModel.FromSource(result);

            var attribute = Assert.Single(model.Attributes);
            Assert.Equal(EAttribute.Strength, attribute.AttributeId);
            Assert.Equal(11m, attribute.Amount);
        }

        private static Enemy MakeEnemy() => new()
        {
            Id = 7,
            Name = "Test Enemy",
            IsBoss = false,
            Level = 3,
            AttributeDistributions =
            [
                new AttributeDistribution
                {
                    AttributeId = EAttribute.Strength,
                    BaseAmount = 5m,
                    AmountPerLevel = 2m,
                },
            ],
            AvailableSkills = [MakeSkill(10), MakeSkill(20)],
        };

        private static Skill MakeSkill(int id) => new()
        {
            Id = id,
            Name = $"Skill {id}",
            BaseDamage = 1,
            Description = string.Empty,
            CooldownMs = 1000,
            DamageMultipliers = [],
            Effects = [],
        };
    }
}
