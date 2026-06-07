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

            enemy.SelectBattleSkills();

            Assert.Equal(4, enemy.Skills.Count);
        }

        [Fact]
        public void SelectBattleSkills_FewerSkillsThanMax_KeepsAll()
        {
            var enemy = MakeEnemy(Skills(3));

            enemy.SelectBattleSkills();

            Assert.Equal([0, 1, 2], enemy.Skills.Select(s => s.Id).OrderBy(id => id));
        }

        [Fact]
        public void SelectBattleSkills_SelectsDistinctSubsetOfAvailableSkills()
        {
            var available = Skills(8);
            var availableIds = available.Select(s => s.Id).ToHashSet();
            var enemy = MakeEnemy(available);

            enemy.SelectBattleSkills();

            Assert.All(enemy.Skills, s => Assert.Contains(s.Id, availableIds));
            Assert.Equal(enemy.Skills.Count, enemy.Skills.Select(s => s.Id).Distinct().Count());
        }

        [Fact]
        public void SetBattleSkills_NarrowsToGivenLoadoutInOrder()
        {
            var enemy = MakeEnemy(Skills(8));

            enemy.SetBattleSkills([5, 1, 7]);

            Assert.Equal([5, 1, 7], enemy.Skills.Select(s => s.Id));
        }

        [Fact]
        public void SetBattleSkills_RoundTripsASelectedLoadout()
        {
            var enemy = MakeEnemy(Skills(8));
            enemy.SelectBattleSkills();
            var selectedIds = enemy.Skills.Select(s => s.Id).ToList();

            // A freshly-built enemy restored from the snapshot reproduces the same loadout.
            var restored = MakeEnemy(Skills(8));
            restored.SetBattleSkills(selectedIds);

            Assert.Equal(selectedIds, restored.Skills.Select(s => s.Id));
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
