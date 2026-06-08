using Game.Core.Zones;
using Xunit;

namespace Game.Core.Tests.Zones
{
    public class ZoneTests
    {
        [Fact]
        public void RollEncounterLevel_AlwaysWithinInclusiveRange()
        {
            var zone = MakeZone(levelMin: 3, levelMax: 7);

            // The roll is random, so sample enough times to exercise the full inclusive range.
            for (var i = 0; i < 1000; i++)
            {
                Assert.InRange(zone.RollEncounterLevel(), 3, 7);
            }
        }

        [Fact]
        public void RollEncounterLevel_SingleLevelRange_AlwaysReturnsThatLevel()
        {
            var zone = MakeZone(levelMin: 5, levelMax: 5);

            for (var i = 0; i < 100; i++)
            {
                Assert.Equal(5, zone.RollEncounterLevel());
            }
        }

        [Fact]
        public void HasBoss_WithBossEnemyId_IsTrue()
        {
            var zone = MakeZone(bossEnemyId: 9);

            Assert.True(zone.HasBoss);
        }

        [Fact]
        public void HasBoss_WithoutBossEnemyId_IsFalse()
        {
            var zone = MakeZone(bossEnemyId: null);

            Assert.False(zone.HasBoss);
        }

        private static Zone MakeZone(
            int levelMin = 1, int levelMax = 10, int? bossEnemyId = null, int bossLevel = 1) => new()
            {
                Id = 0,
                Name = "Test Zone",
                LevelMin = levelMin,
                LevelMax = levelMax,
                BossEnemyId = bossEnemyId,
                BossLevel = bossLevel,
            };
    }
}
