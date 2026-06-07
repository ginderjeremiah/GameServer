using Game.Core.Battle;
using Game.Core.Enemies;
using Game.Core.Skills;
using Xunit;

namespace Game.Core.Tests.Battle
{
    public class BattleFactoryTests
    {
        private readonly BattleFactory _factory = new();

        [Fact]
        public void CreateBattleEnemy_RollsLevelWithinZoneRange()
        {
            const int min = 3;
            const int max = 7;

            // The level is rolled at random, so sample enough times to exercise the full range.
            for (var i = 0; i < 1000; i++)
            {
                var enemy = _factory.CreateBattleEnemy(min, max, resolveEnemy: MakeEnemyAtLevel);

                Assert.InRange(enemy.Level, min, max);
            }
        }

        [Fact]
        public void CreateBattleEnemy_SingleLevelRange_UsesThatLevel()
        {
            var enemy = _factory.CreateBattleEnemy(5, 5, resolveEnemy: MakeEnemyAtLevel);

            Assert.Equal(5, enemy.Level);
        }

        [Fact]
        public void CreateBattleEnemy_ResolvesEnemyAtRolledLevel()
        {
            int? resolvedLevel = null;

            var enemy = _factory.CreateBattleEnemy(2, 2, resolveEnemy: level =>
            {
                resolvedLevel = level;
                return MakeEnemyAtLevel(level);
            });

            Assert.Equal(2, resolvedLevel);
            Assert.Equal(resolvedLevel, enemy.Level);
        }

        [Fact]
        public void CreateBattleEnemy_SelectsBattleSkillsForResolvedEnemy()
        {
            var enemy = _factory.CreateBattleEnemy(1, 1, resolveEnemy: level => MakeEnemyAtLevel(level, skillCount: 6));

            Assert.Equal(4, enemy.BattleSkills.Count);
        }

        [Fact]
        public void CreateBattleEnemy_ReturnsTheResolvedEnemyInstance()
        {
            var resolved = MakeEnemyAtLevel(1, skillCount: 2);

            var returned = _factory.CreateBattleEnemy(1, 1, resolveEnemy: _ => resolved);

            Assert.Same(resolved, returned);
        }

        private static Enemy MakeEnemyAtLevel(int level) => MakeEnemyAtLevel(level, skillCount: 1);

        private static Enemy MakeEnemyAtLevel(int level, int skillCount) => new()
        {
            Id = 1,
            Name = "Test Enemy",
            IsBoss = false,
            Level = level,
            AttributeDistributions = [],
            AvailableSkills = [.. Enumerable.Range(0, skillCount).Select(MakeSkill)],
        };

        private static Skill MakeSkill(int id) => new()
        {
            Id = id,
            Name = $"Skill {id}",
            Description = "",
            BaseDamage = 1,
            CooldownMs = 1000,
            DamageMultipliers = [],
        };
    }
}
