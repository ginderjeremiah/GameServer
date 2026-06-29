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
        public void SelectBattleSkills_OverManyRuns_AlwaysYieldsADistinctCappedSubset()
        {
            // The selection uses Random.Shared (no injectable seam), so rather than assert an exact
            // permutation we assert the invariants hold on every run: capped count, drawn from the
            // available pool, and no duplicates.
            var available = Skills(8);
            var availableIds = available.Select(s => s.Id).ToHashSet();
            var enemy = MakeEnemy(available);

            for (var run = 0; run < 1000; run++)
            {
                enemy.SelectBattleSkills();
                var selectedIds = enemy.BattleSkills.Select(s => s.Id).ToList();

                Assert.Equal(4, selectedIds.Count);
                Assert.All(selectedIds, id => Assert.Contains(id, availableIds));
                Assert.Equal(selectedIds.Count, selectedIds.Distinct().Count());
            }
        }

        [Fact]
        public void SelectBattleSkills_OverManyRuns_CanSelectEverySkillAndEveryPosition()
        {
            // A degenerate or structurally-biased selection would systematically exclude some skill or
            // never place a skill in some slot. Across many runs an unbiased partial Fisher–Yates must
            // reach every available skill, in every loadout position.
            var available = Skills(8);
            var enemy = MakeEnemy(available);
            var seenSkillIds = new HashSet<int>();
            var seenAtPosition = new HashSet<int>[4];
            for (var pos = 0; pos < seenAtPosition.Length; pos++)
            {
                seenAtPosition[pos] = [];
            }

            for (var run = 0; run < 2000; run++)
            {
                enemy.SelectBattleSkills();
                var selectedIds = enemy.BattleSkills.Select(s => s.Id).ToList();
                for (var pos = 0; pos < selectedIds.Count; pos++)
                {
                    seenSkillIds.Add(selectedIds[pos]);
                    seenAtPosition[pos].Add(selectedIds[pos]);
                }
            }

            Assert.Equal(8, seenSkillIds.Count);
            Assert.All(seenAtPosition, positionIds => Assert.Equal(8, positionIds.Count));
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
            // The dedicated-boss loadout is the full authored set — neither capped at MaxSelectedSkills (4)
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
            DamageType = EDamageType.Physical,
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
