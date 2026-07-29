using Game.Core.Zones;
using Xunit;

namespace Game.Core.Tests.Zones
{
    /// <summary>
    /// The encounter-level rules the <see cref="Zone"/> constructor and the content-authoring save both read.
    /// <see cref="ZoneTests"/> pins the throwing side; these pin the predicates themselves, so the authoring
    /// guard and the domain invariant can't drift apart.
    /// </summary>
    public class ZoneLevelRulesTests
    {
        [Theory]
        [InlineData(int.MinValue, false)]
        [InlineData(-1, false)]
        [InlineData(0, false)]
        [InlineData(1, true)]
        [InlineData(50, true)]
        public void IsValidLevel_AcceptsOnlyLevelsAtOrAboveTheMinimum(int level, bool expected)
        {
            Assert.Equal(expected, ZoneLevelRules.IsValidLevel(level));
        }

        [Fact]
        public void IsValidLevel_AcceptsTheMinimumItself()
        {
            Assert.True(ZoneLevelRules.IsValidLevel(ZoneLevelRules.MinZoneLevel));
            Assert.False(ZoneLevelRules.IsValidLevel(ZoneLevelRules.MinZoneLevel - 1));
        }

        [Theory]
        [InlineData(1, 10, true)]
        [InlineData(5, 5, true)]
        [InlineData(8, 4, false)]
        [InlineData(2, 1, false)]
        public void IsOrderedRange_AllowsEqualBoundsButNotAnInvertedRange(int levelMin, int levelMax, bool expected)
        {
            Assert.Equal(expected, ZoneLevelRules.IsOrderedRange(levelMin, levelMax));
        }
    }
}
