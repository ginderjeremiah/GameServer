using Game.Core.Battle;
using Game.Core.Enemies;
using Game.Core.Skills;
using Game.Core.Zones;
using Xunit;

namespace Game.Core.Tests.Battle
{
    public class BattleFactoryTests
    {
        private readonly BattleFactory _factory = new();

        [Fact]
        public void CreateBattleEnemy_RollsLevelWithinZoneRange()
        {
            var zone = MakeZone(levelMin: 3, levelMax: 7);

            // The level is rolled at random, so sample enough times to exercise the full range.
            for (var i = 0; i < 1000; i++)
            {
                var enemy = _factory.CreateBattleEnemy(zone, resolveEnemy: MakeEnemyAtLevel);

                Assert.InRange(enemy.Level, 3, 7);
            }
        }

        [Fact]
        public void CreateBattleEnemy_SingleLevelRange_UsesThatLevel()
        {
            var enemy = _factory.CreateBattleEnemy(MakeZone(levelMin: 5, levelMax: 5), resolveEnemy: MakeEnemyAtLevel);

            Assert.Equal(5, enemy.Level);
        }

        [Fact]
        public void CreateBattleEnemy_ResolvesEnemyAtRolledLevel()
        {
            int? resolvedLevel = null;

            var enemy = _factory.CreateBattleEnemy(MakeZone(levelMin: 2, levelMax: 2), resolveEnemy: level =>
            {
                resolvedLevel = level;
                return MakeEnemyAtLevel(level);
            });

            Assert.Equal(2, resolvedLevel);
            Assert.Equal(resolvedLevel, enemy.Level);
        }

        [Fact]
        public void CreateBattleEnemy_SelectsCappedBattleSkillsForResolvedEnemy()
        {
            var enemy = _factory.CreateBattleEnemy(
                MakeZone(levelMin: 1, levelMax: 1), resolveEnemy: level => MakeEnemyAtLevel(level, skillCount: 6));

            // The random idle encounter caps the loadout at the MaxSelectedSkills limit (4).
            Assert.Equal(4, enemy.BattleSkills.Count);
        }

        [Fact]
        public void CreateBattleEnemy_ReturnsTheResolvedEnemyInstance()
        {
            var resolved = MakeEnemyAtLevel(1, skillCount: 2);

            var returned = _factory.CreateBattleEnemy(MakeZone(levelMin: 1, levelMax: 1), resolveEnemy: _ => resolved);

            Assert.Same(resolved, returned);
        }

        [Fact]
        public void CreateBossEnemy_ResolvesEnemyAtFixedBossLevel()
        {
            int? resolvedLevel = null;
            var zone = MakeZone(levelMin: 1, levelMax: 1, bossEnemyId: 9, bossLevel: 18);

            var enemy = _factory.CreateBossEnemy(zone, resolveEnemy: level =>
            {
                resolvedLevel = level;
                return MakeEnemyAtLevel(level);
            });

            // Deterministic: the boss is always built at the zone's fixed BossLevel, never a rolled level.
            Assert.Equal(18, resolvedLevel);
            Assert.Equal(18, enemy.Level);
        }

        [Fact]
        public void CreateBossEnemy_UsesFullAuthoredLoadout_NotCapped()
        {
            var zone = MakeZone(levelMin: 1, levelMax: 1, bossEnemyId: 9, bossLevel: 5);

            var enemy = _factory.CreateBossEnemy(zone, resolveEnemy: level => MakeEnemyAtLevel(level, skillCount: 6));

            // Unlike the random encounter, the boss brings its entire authored loadout (no 4-skill cap).
            Assert.Equal(6, enemy.BattleSkills.Count);
        }

        [Fact]
        public void CreateBossEnemy_ReturnsTheResolvedEnemyInstance()
        {
            var resolved = MakeEnemyAtLevel(5, skillCount: 2);

            var returned = _factory.CreateBossEnemy(
                MakeZone(levelMin: 1, levelMax: 1, bossEnemyId: 9, bossLevel: 5), resolveEnemy: _ => resolved);

            Assert.Same(resolved, returned);
        }

        private static Zone MakeZone(int levelMin, int levelMax, int? bossEnemyId = null, int bossLevel = 1) => new()
        {
            Id = 0,
            LevelMin = levelMin,
            LevelMax = levelMax,
            BossEnemyId = bossEnemyId,
            BossLevel = bossLevel,
            UnlockChallengeId = null,
        };

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
            DamagePortions = [new SkillDamagePortion { Type = EDamageType.Physical, Weight = 1.0 }],
            BaseDamage = 1,
            CooldownMs = 1000,
            DamageMultipliers = [],
            Effects = [],
        };
    }
}
