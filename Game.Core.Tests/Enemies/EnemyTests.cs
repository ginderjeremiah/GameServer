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

            Assert.Equal(4, enemy.BattleSkills.Count);
        }

        [Fact]
        public void SelectBattleSkills_FewerSkillsThanMax_KeepsAll()
        {
            var enemy = MakeEnemy(Skills(3));

            enemy.SelectBattleSkills();

            Assert.Equal([0, 1, 2], enemy.BattleSkills.Select(s => s.Id).OrderBy(id => id));
        }

        [Fact]
        public void SelectBattleSkills_SelectsDistinctSubsetOfAvailableSkills()
        {
            var available = Skills(8);
            var availableIds = available.Select(s => s.Id).ToHashSet();
            var enemy = MakeEnemy(available);

            enemy.SelectBattleSkills();

            Assert.All(enemy.BattleSkills, s => Assert.Contains(s.Id, availableIds));
            Assert.Equal(enemy.BattleSkills.Count, enemy.BattleSkills.Select(s => s.Id).Distinct().Count());
        }

        [Fact]
        public void SelectBattleSkills_DoesNotMutateAvailableSkills()
        {
            var enemy = MakeEnemy(Skills(6));

            enemy.SelectBattleSkills();

            Assert.Equal(6, enemy.AvailableSkills.Count);
        }

        [Fact]
        public void SelectAllBattleSkills_KeepsEveryAvailableSkillInAuthoredOrder()
        {
            // The dedicated-boss loadout is the full authored set — neither capped at MaxBattleSkills (4)
            // nor shuffled — so a 6-skill boss brings all 6 in order.
            var enemy = MakeEnemy(Skills(6));

            enemy.SelectAllBattleSkills();

            Assert.Equal([0, 1, 2, 3, 4, 5], enemy.BattleSkills.Select(s => s.Id));
        }

        [Fact]
        public void SelectAllBattleSkills_DoesNotMutateAvailableSkills()
        {
            var enemy = MakeEnemy(Skills(6));

            enemy.SelectAllBattleSkills();

            Assert.Equal(6, enemy.AvailableSkills.Count);
        }

        [Fact]
        public void SetBattleSkills_NarrowsToGivenLoadoutInOrder()
        {
            var enemy = MakeEnemy(Skills(8));

            enemy.SetBattleSkills([5, 1, 7]);

            Assert.Equal([5, 1, 7], enemy.BattleSkills.Select(s => s.Id));
        }

        [Fact]
        public void SetBattleSkills_DoesNotMutateAvailableSkills()
        {
            var enemy = MakeEnemy(Skills(8));

            enemy.SetBattleSkills([5, 1, 7]);

            Assert.Equal(8, enemy.AvailableSkills.Count);
        }

        [Fact]
        public void SetBattleSkills_RoundTripsASelectedLoadout()
        {
            var enemy = MakeEnemy(Skills(8));
            enemy.SelectBattleSkills();
            var selectedIds = enemy.BattleSkills.Select(s => s.Id).ToList();

            // A freshly-built enemy restored from the snapshot reproduces the same loadout.
            var restored = MakeEnemy(Skills(8));
            restored.SetBattleSkills(selectedIds);

            Assert.Equal(selectedIds, restored.BattleSkills.Select(s => s.Id));
        }

        [Fact]
        public void BattleSkills_BeforeSelection_ThrowsInvalidOperation()
        {
            var enemy = MakeEnemy(Skills(3));

            Assert.Throws<InvalidOperationException>(() => _ = enemy.BattleSkills);
        }

        [Fact]
        public void SetBattleSkills_UnknownSkillId_ThrowsDescriptiveInvalidOperation()
        {
            // A snapshot id no longer among the enemy's available skills (e.g. SetEnemySkills changed the
            // loadout between battle start and validation) fails meaningfully rather than with a bare
            // KeyNotFoundException from the dictionary indexer.
            var enemy = MakeEnemy(Skills(3));

            var ex = Assert.Throws<InvalidOperationException>(() => enemy.SetBattleSkills([0, 99]));
            Assert.Contains("99", ex.Message);
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
            Effects = [],
        };

        private static Enemy MakeEnemy(List<Skill> skills) => new()
        {
            Id = 1,
            Name = "Test Enemy",
            IsBoss = false,
            Level = 1,
            AttributeDistributions = [],
            AvailableSkills = skills,
        };
    }
}
