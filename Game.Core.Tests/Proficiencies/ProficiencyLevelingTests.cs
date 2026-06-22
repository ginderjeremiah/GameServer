using Game.Core.Proficiencies;
using Xunit;

namespace Game.Core.Tests.Proficiencies
{
    /// <summary>
    /// The proficiency XP curve and multi-level application (spike #982 area C). XP is the residual within the
    /// current level, leveling spans as many thresholds as a gain covers, and a maxed proficiency banks no
    /// overflow. The curve is <c>BaseXp × XpGrowth^level</c>, so with BaseXp 100 / growth 2 the thresholds are
    /// 100, 200, 400, … per successive level.
    /// </summary>
    public class ProficiencyLevelingTests
    {
        [Theory]
        [InlineData(0, 100)]
        [InlineData(1, 200)]
        [InlineData(2, 400)]
        [InlineData(3, 800)]
        public void XpForLevel_FollowsTheGeometricCurve(int level, int expected)
        {
            var proficiency = Make(baseXp: 100, xpGrowth: 2);
            Assert.Equal(expected, proficiency.XpForLevel(level));
        }

        [Fact]
        public void XpForLevel_FlatGrowth_IsConstantPerLevel()
        {
            var proficiency = Make(baseXp: 100, xpGrowth: 1);
            Assert.Equal(100m, proficiency.XpForLevel(0));
            Assert.Equal(100m, proficiency.XpForLevel(5));
        }

        [Fact]
        public void ApplyXp_BelowThreshold_AccruesWithoutLeveling()
        {
            var proficiency = Make(baseXp: 100, xpGrowth: 2);
            Assert.Equal((0, 50m), proficiency.ApplyXp(currentLevel: 0, currentXp: 0, xpGain: 50));
        }

        [Fact]
        public void ApplyXp_ExactlyAtThreshold_LevelsWithNoCarryover()
        {
            var proficiency = Make(baseXp: 100, xpGrowth: 2);
            Assert.Equal((1, 0m), proficiency.ApplyXp(currentLevel: 0, currentXp: 0, xpGain: 100));
        }

        [Fact]
        public void ApplyXp_OverThreshold_LevelsWithCarryover()
        {
            var proficiency = Make(baseXp: 100, xpGrowth: 2);
            // 150 - 100 (threshold for level 0) = 50 residual into level 1.
            Assert.Equal((1, 50m), proficiency.ApplyXp(currentLevel: 0, currentXp: 0, xpGain: 150));
        }

        [Fact]
        public void ApplyXp_SpansMultipleLevelsInOneGain()
        {
            var proficiency = Make(baseXp: 100, xpGrowth: 2);
            // 350: consumes 100 (→L1) and 200 (→L2), leaving 50 residual in level 2.
            Assert.Equal((2, 50m), proficiency.ApplyXp(currentLevel: 0, currentXp: 0, xpGain: 350));
        }

        [Fact]
        public void ApplyXp_FromMidLevelWithExistingXp_ConsumesTheCurrentThreshold()
        {
            var proficiency = Make(baseXp: 100, xpGrowth: 2);
            // Level 1 (threshold 200) starting at 50 + gain 200 = 250 → cross 200 to level 2 with 50 residual.
            Assert.Equal((2, 50m), proficiency.ApplyXp(currentLevel: 1, currentXp: 50, xpGain: 200));
        }

        [Fact]
        public void ApplyXp_GainBeyondMaxLevel_CapsLevelAndDropsOverflow()
        {
            var proficiency = Make(baseXp: 100, xpGrowth: 2, maxLevel: 3);
            // Full climb 0→3 costs 100 + 200 + 400 = 700; a 1000 gain caps at level 3 and banks no residual.
            Assert.Equal((3, 0m), proficiency.ApplyXp(currentLevel: 0, currentXp: 0, xpGain: 1000));
        }

        [Fact]
        public void ApplyXp_AlreadyAtMaxLevel_IsANoOpAtTheCap()
        {
            var proficiency = Make(baseXp: 100, xpGrowth: 2, maxLevel: 3);
            Assert.Equal((3, 0m), proficiency.ApplyXp(currentLevel: 3, currentXp: 0, xpGain: 500));
        }

        [Fact]
        public void MilestonesCrossed_ReturnsAuthoredPayoutLevelsInTheOpenClosedRange()
        {
            var proficiency = Make(baseXp: 100, xpGrowth: 2, payoutLevels: [5, 10]);

            Assert.Equal([5], proficiency.MilestonesCrossed(fromLevel: 3, toLevel: 7));
            Assert.Equal([5, 10], proficiency.MilestonesCrossed(fromLevel: 0, toLevel: 10));
            // The lower bound is exclusive (already reached), the upper inclusive (newly reached).
            Assert.Equal([5], proficiency.MilestonesCrossed(fromLevel: 4, toLevel: 5));
            Assert.Empty(proficiency.MilestonesCrossed(fromLevel: 5, toLevel: 5));
            Assert.Empty(proficiency.MilestonesCrossed(fromLevel: 6, toLevel: 9));
        }

        private static Proficiency Make(double baseXp, double xpGrowth, int maxLevel = 10, int[]? payoutLevels = null) => new()
        {
            Id = 0,
            Name = "Test",
            Description = string.Empty,
            MaxLevel = maxLevel,
            BaseXp = baseXp,
            XpGrowth = xpGrowth,
            StartsUnlocked = true,
            SeedSkillId = null,
            PrerequisiteIds = [],
            Levels = (payoutLevels ?? []).Select(level => new ProficiencyLevel
            {
                Level = level,
                Modifiers = [],
                RewardSkillId = null,
            }).ToList(),
        };
    }
}
