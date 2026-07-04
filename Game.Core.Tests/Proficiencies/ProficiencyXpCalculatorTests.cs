using Game.Core.Proficiencies;
using Xunit;
using static Game.Core.Proficiencies.ProficiencyXpCalculator;

namespace Game.Core.Tests.Proficiencies
{
    /// <summary>
    /// The effect-based per-path XP claim (spike #1318, max-normalized per spike #1526 Decision 5): each path
    /// independently earns <c>pie × activity ÷ max(playerRating, enemyRating)</c>. The claims overlap and need
    /// <em>not</em> sum to 1 — there is no shared pie to split. These pin the documented properties:
    /// max-normalization (the denominator is whichever rating is larger), no clamp (a huge activity relative to
    /// a small normalizer pays out proportionally rather than saturating), a trivial fight pays little
    /// (anti-grind), independent parallel claims across paths, and the degenerate guards (non-positive
    /// normalizer/activity).
    /// </summary>
    public class ProficiencyXpCalculatorTests
    {
        private const double Pie = 10.0;

        [Fact]
        public void Split_ActivityEqualToNormalizer_EarnsTheWholePie()
        {
            // activity == max(playerRating, enemyRating) → ratio 1 → the full pie.
            var slice = Assert.Single(Split(Pie, playerRating: 100, enemyRating: 50, [new PathActivity(0, Activity: 100)]));
            Assert.Equal(0, slice.ProficiencyId);
            Assert.Equal(Pie, slice.Xp, precision: 9);
        }

        [Fact]
        public void Split_ActivityBelowNormalizer_EarnsProportionally()
        {
            // A trivial fight: activity a quarter of the normalizer → a quarter of the pie (anti-grind).
            var slice = Assert.Single(Split(Pie, playerRating: 100, enemyRating: 50, [new PathActivity(0, Activity: 25)]));
            Assert.Equal(Pie * 0.25, slice.Xp, precision: 9);
        }

        [Fact]
        public void Split_ActivityAboveNormalizer_EarnsMore_NoClamp()
        {
            // Over-level / heavy-hitting: activity twice the normalizer → twice the pie. There is no clamp —
            // pathActivity ≤ enemyHP already bounds the claim naturally (spike #1526 Decision 5).
            var slice = Assert.Single(Split(Pie, playerRating: 100, enemyRating: 50, [new PathActivity(0, Activity: 200)]));
            Assert.Equal(Pie * 2.0, slice.Xp, precision: 9);
        }

        [Fact]
        public void Split_HugeActivityRelativeToNormalizer_IsNotCapped()
        {
            // A huge activity relative to a small normalizer produces a correspondingly huge XP slice — the
            // retired MaxExpRewardMultiplier clamp is gone.
            var slice = Assert.Single(Split(Pie, playerRating: 1, enemyRating: 1, [new PathActivity(0, Activity: 1_000_000)]));
            Assert.Equal(Pie * 1_000_000, slice.Xp, precision: 3);
        }

        [Theory]
        [InlineData(100, 50, 100)]  // player is the larger rating → normalizes by player
        [InlineData(50, 100, 100)]  // enemy is the larger rating → normalizes by enemy
        public void Split_NormalizesByWhicheverRatingIsLarger(double playerRating, double enemyRating, double normalizer)
        {
            var slice = Assert.Single(Split(Pie, playerRating, enemyRating, [new PathActivity(0, Activity: normalizer)]));
            Assert.Equal(Pie, slice.Xp, precision: 9);
        }

        [Fact]
        public void Split_MultiplePaths_ClaimIndependently_NeedNotSumToOne()
        {
            // Two paths each with activity == normalizer: each earns the FULL pie. There is no shared pie to
            // dilute — the claims overlap, so a multi-axis build mints more total proficiency XP than a
            // single-axis one.
            var slices = Split(Pie, playerRating: 100, enemyRating: 50,
                [new PathActivity(0, 100), new PathActivity(1, 100)]);

            Assert.Equal(Pie, slices.Single(s => s.ProficiencyId == 0).Xp, precision: 9);
            Assert.Equal(Pie, slices.Single(s => s.ProficiencyId == 1).Xp, precision: 9);
        }

        [Fact]
        public void Split_FocusBeatsSpread_ViaActivityShare()
        {
            // Focus vs spread lives in the activity the caller resolves per path: a focused path captures the
            // full damage (100), a dabbled one a fraction (25), so it trains at a quarter the rate.
            var slices = Split(Pie, playerRating: 100, enemyRating: 50,
                [new PathActivity(0, 100), new PathActivity(1, 25)]);

            Assert.Equal(Pie, slices.Single(s => s.ProficiencyId == 0).Xp, precision: 9);
            Assert.Equal(Pie * 0.25, slices.Single(s => s.ProficiencyId == 1).Xp, precision: 9);
        }

        [Fact]
        public void Split_OmitsNonPositiveActivity()
        {
            // A path with zero activity earns nothing and is omitted (not a zero slice).
            var slices = Split(Pie, playerRating: 100, enemyRating: 50,
                [new PathActivity(0, 100), new PathActivity(1, 0)]);

            Assert.Equal(0, Assert.Single(slices).ProficiencyId);
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(-5, -5)]
        [InlineData(0, -5)]
        public void Split_NonPositiveNormalizer_ReturnsEmpty(double playerRating, double enemyRating)
        {
            // A degenerate non-positive normalizer (CombatRating.Rate always returns a strictly-positive value
            // in production) yields no slices rather than dividing by zero.
            Assert.Empty(Split(Pie, playerRating, enemyRating, [new PathActivity(0, 100)]));
        }

        [Fact]
        public void Split_NoActivities_ReturnsEmpty()
        {
            Assert.Empty(Split(Pie, playerRating: 100, enemyRating: 50, []));
        }

        [Fact]
        public void Split_OrdersSlicesByProficiencyId()
        {
            var slices = Split(Pie, playerRating: 100, enemyRating: 50,
                [new PathActivity(5, 100), new PathActivity(2, 100), new PathActivity(9, 100)]);

            Assert.Equal([2, 5, 9], slices.Select(s => s.ProficiencyId));
        }

        [Theory]
        [InlineData(50, Pie * 0.5)]
        [InlineData(100, Pie)]
        [InlineData(200, Pie * 2.0)]
        [InlineData(400, Pie * 4.0)] // no clamp — scales linearly past the old 4x cap
        public void Split_ScalesLinearlyWithActivityOverNormalizer(double activity, double expectedXp)
        {
            var slice = Assert.Single(Split(Pie, playerRating: 100, enemyRating: 50, [new PathActivity(0, activity)]));
            Assert.Equal(expectedXp, slice.Xp, precision: 9);
        }
    }
}
