using Game.Core.Proficiencies;
using Xunit;
using static Game.Core.Proficiencies.ProficiencyXpCalculator;

namespace Game.Core.Tests.Proficiencies
{
    /// <summary>
    /// The effect-based per-path XP claim (spike #1318, max-normalized per spike #1526 Decision 5): each path
    /// independently earns <c>pie × activity ÷ ratingDenominator</c>. The claims overlap and need <em>not</em>
    /// sum to 1 — there is no shared pie to split. These pin the documented properties: magnitude tracks activity
    /// relative to the rating denominator with no clamp (activity is bounded upstream by the enemy's health
    /// pool instead), a trivial fight pays little (anti-grind), independent parallel claims across paths, and
    /// the degenerate guards (non-positive denominator/activity).
    /// </summary>
    public class ProficiencyXpCalculatorTests
    {
        private const double Pie = 10.0;

        [Fact]
        public void Split_ActivityEqualToRatingDenominator_EarnsTheWholePie()
        {
            // activity == ratingDenominator → ratio 1 → the full pie.
            var slice = Assert.Single(Split(Pie, ratingDenominator: 100, [new PathActivity(0, Activity: 100)]));
            Assert.Equal(0, slice.ProficiencyId);
            Assert.Equal(Pie, slice.Xp, precision: 9);
        }

        [Fact]
        public void Split_ActivityBelowRatingDenominator_EarnsProportionally()
        {
            // A trivial fight: activity a quarter of the denominator → a quarter of the pie (anti-grind).
            var slice = Assert.Single(Split(Pie, ratingDenominator: 100, [new PathActivity(0, Activity: 25)]));
            Assert.Equal(Pie * 0.25, slice.Xp, precision: 9);
        }

        [Fact]
        public void Split_ActivityAboveRatingDenominator_EarnsMoreWithNoClamp()
        {
            // Above your weight: activity far beyond the denominator scales linearly with no clamp — the old
            // MaxExpRewardMultiplier-mirroring clamp retired under max-normalization (spike #1526 Decision 5),
            // since activity is itself bounded upstream by the enemy's health pool (#1482).
            var slice = Assert.Single(Split(Pie, ratingDenominator: 100, [new PathActivity(0, Activity: 1_000_000)]));
            Assert.Equal(Pie * 10_000.0, slice.Xp, precision: 6);
        }

        [Fact]
        public void Split_MultiplePaths_ClaimIndependently_NeedNotSumToOne()
        {
            // Two paths each with activity == ratingDenominator: each earns the FULL pie. There is no shared pie
            // to dilute — the claims overlap, so a multi-axis build mints more total proficiency XP than a
            // single-axis one.
            var slices = Split(Pie, ratingDenominator: 100,
                [new PathActivity(0, 100), new PathActivity(1, 100)]);

            Assert.Equal(Pie, slices.Single(s => s.ProficiencyId == 0).Xp, precision: 9);
            Assert.Equal(Pie, slices.Single(s => s.ProficiencyId == 1).Xp, precision: 9);
        }

        [Fact]
        public void Split_FocusBeatsSpread_ViaActivityShare()
        {
            // Focus vs spread lives in the activity the caller resolves per path: a focused path captures the
            // full damage (100), a dabbled one a fraction (25), so it trains at a quarter the rate.
            var slices = Split(Pie, ratingDenominator: 100,
                [new PathActivity(0, 100), new PathActivity(1, 25)]);

            Assert.Equal(Pie, slices.Single(s => s.ProficiencyId == 0).Xp, precision: 9);
            Assert.Equal(Pie * 0.25, slices.Single(s => s.ProficiencyId == 1).Xp, precision: 9);
        }

        [Fact]
        public void Split_OmitsNonPositiveActivity()
        {
            // A path with zero activity earns nothing and is omitted (not a zero slice).
            var slices = Split(Pie, ratingDenominator: 100,
                [new PathActivity(0, 100), new PathActivity(1, 0)]);

            Assert.Equal(0, Assert.Single(slices).ProficiencyId);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        public void Split_NonPositiveRatingDenominator_ReturnsEmpty(double ratingDenominator)
        {
            // A degenerate non-positive denominator (a real battler's CombatRating.Rate is always strictly
            // positive) yields no slices rather than dividing by zero.
            Assert.Empty(Split(Pie, ratingDenominator, [new PathActivity(0, 100)]));
        }

        [Fact]
        public void Split_NoActivities_ReturnsEmpty()
        {
            Assert.Empty(Split(Pie, ratingDenominator: 100, []));
        }

        [Fact]
        public void Split_OrdersSlicesByProficiencyId()
        {
            var slices = Split(Pie, ratingDenominator: 100,
                [new PathActivity(5, 100), new PathActivity(2, 100), new PathActivity(9, 100)]);

            Assert.Equal([2, 5, 9], slices.Select(s => s.ProficiencyId));
        }

        [Theory]
        [InlineData(50, Pie * 0.5)]
        [InlineData(100, Pie)]
        [InlineData(200, Pie * 2.0)]
        [InlineData(400, Pie * 4.0)]
        public void Split_ScalesLinearlyWithActivityOverRatingDenominator(double activity, double expectedXp)
        {
            var slice = Assert.Single(Split(Pie, ratingDenominator: 100, [new PathActivity(0, activity)]));
            Assert.Equal(expectedXp, slice.Xp, precision: 9);
        }
    }
}
