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
        public void IsUnlocked_UngatedZone_IsAlwaysUnlocked()
        {
            var zone = MakeZone(unlockChallengeId: null);

            Assert.True(zone.IsUnlocked(new HashSet<int>()));
            Assert.True(zone.IsUnlocked(new HashSet<int> { 1, 2, 3 }));
        }

        [Fact]
        public void IsUnlocked_GatedZone_RequiresTheGatingChallengeToBeCompleted()
        {
            var zone = MakeZone(unlockChallengeId: 7);

            Assert.False(zone.IsUnlocked(new HashSet<int>()));
            Assert.False(zone.IsUnlocked(new HashSet<int> { 6, 8 }));
            Assert.True(zone.IsUnlocked(new HashSet<int> { 6, 7, 8 }));
        }

        [Fact]
        public void Construct_ValidRange_Succeeds()
        {
            var zone = MakeZone(levelMin: 3, levelMax: 7);

            Assert.Equal(3, zone.LevelMin);
            Assert.Equal(7, zone.LevelMax);
        }

        [Fact]
        public void Construct_EqualBounds_IsAllowed()
        {
            var zone = MakeZone(levelMin: 5, levelMax: 5);

            Assert.Equal(5, zone.LevelMin);
            Assert.Equal(5, zone.LevelMax);
        }

        [Fact]
        public void Construct_LevelMinGreaterThanLevelMax_Throws()
        {
            var ex = Assert.Throws<ArgumentException>(() => MakeZone(levelMin: 8, levelMax: 4));

            Assert.Contains("8", ex.Message);
            Assert.Contains("4", ex.Message);
            Assert.Equal(nameof(Zone.LevelMin), ex.ParamName);
        }

        // The cross-field invariant must hold regardless of which bound the object initializer assigns
        // first, so pin the inverted-assignment path too (LevelMax set before LevelMin).
        [Fact]
        public void Construct_LevelMinGreaterThanLevelMax_ThrowsWhenMaxAssignedFirst()
        {
            var ex = Assert.Throws<ArgumentException>(() => new Zone
            {
                Id = 0,
                LevelMax = 4,
                LevelMin = 8,
                BossEnemyId = null,
                BossLevel = 1,
                UnlockChallengeId = null,
            });

            Assert.Contains("8", ex.Message);
            Assert.Contains("4", ex.Message);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Construct_LevelMinBelowOne_Throws(int levelMin)
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => MakeZone(levelMin: levelMin, levelMax: 10));

            Assert.Equal(nameof(Zone.LevelMin), ex.ParamName);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Construct_LevelMaxBelowOne_Throws(int levelMax)
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => MakeZone(levelMin: 1, levelMax: levelMax));

            Assert.Equal(nameof(Zone.LevelMax), ex.ParamName);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Construct_BossLevelBelowOne_Throws(int bossLevel)
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => MakeZone(bossLevel: bossLevel));

            Assert.Equal(nameof(Zone.BossLevel), ex.ParamName);
        }

        private static Zone MakeZone(
            int levelMin = 1, int levelMax = 10, int? bossEnemyId = null, int bossLevel = 1,
            int? unlockChallengeId = null) => new()
            {
                Id = 0,
                LevelMin = levelMin,
                LevelMax = levelMax,
                BossEnemyId = bossEnemyId,
                BossLevel = bossLevel,
                UnlockChallengeId = unlockChallengeId,
            };
    }
}
