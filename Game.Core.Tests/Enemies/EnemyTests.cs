using Game.Core.Enemies;
using Game.Core.Skills;
using Xunit;

namespace Game.Core.Tests.Enemies
{
    public class EnemyTests
    {
        [Fact]
        public void SelectBattleSkills_MoreSkillsThanMax_NarrowsToFour()
        {
            var enemy = MakeEnemy(Skills(6));

            enemy.SelectBattleSkills(12345);

            Assert.Equal(4, enemy.Skills.Count);
        }

        [Fact]
        public void SelectBattleSkills_FewerSkillsThanMax_KeepsAll()
        {
            var enemy = MakeEnemy(Skills(3));

            enemy.SelectBattleSkills(12345);

            Assert.Equal([0, 1, 2], enemy.Skills.Select(s => s.Id).OrderBy(id => id));
        }

        [Fact]
        public void SelectBattleSkills_SelectsDistinctSubsetOfAvailableSkills()
        {
            var available = Skills(8);
            var availableIds = available.Select(s => s.Id).ToHashSet();
            var enemy = MakeEnemy(available);

            enemy.SelectBattleSkills(999);

            Assert.All(enemy.Skills, s => Assert.Contains(s.Id, availableIds));
            Assert.Equal(enemy.Skills.Count, enemy.Skills.Select(s => s.Id).Distinct().Count());
        }

        [Fact]
        public void SelectBattleSkills_SameSeed_ProducesSameLoadout()
        {
            var enemyA = MakeEnemy(Skills(8));
            var enemyB = MakeEnemy(Skills(8));

            enemyA.SelectBattleSkills(42);
            enemyB.SelectBattleSkills(42);

            Assert.Equal(enemyA.Skills.Select(s => s.Id), enemyB.Skills.Select(s => s.Id));
        }

        private static List<Skill> Skills(int count) =>
            [.. Enumerable.Range(0, count).Select(MakeSkill)];

        private static Skill MakeSkill(int id) => new()
        {
            Id = id,
            Name = $"Skill {id}",
            Description = "",
            BaseDamage = 1,
            CooldownMs = 1000,
            DamageMultipliers = [],
        };

        private static Enemy MakeEnemy(List<Skill> skills) => new()
        {
            Id = 1,
            Name = "Test Enemy",
            IsBoss = false,
            Level = 1,
            AttributeDistributions = [],
            Skills = skills,
        };
    }
}
