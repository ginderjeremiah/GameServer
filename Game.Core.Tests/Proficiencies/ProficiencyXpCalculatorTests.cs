using Game.Core.Proficiencies;
using Xunit;
using static Game.Core.Proficiencies.ProficiencyXpCalculator;

namespace Game.Core.Tests.Proficiencies
{
    /// <summary>
    /// The effect-based per-path XP claim (spike #1318): each path independently earns
    /// <c>pie × clamp(activity ÷ power, max)</c>. The claims overlap and need <em>not</em> sum to 1 — there is
    /// no shared pie to split. These pin the documented properties: magnitude tracks activity relative to power,
    /// the clamp mirrors <c>MaxExpRewardMultiplier</c>, a trivial fight pays little (anti-grind), independent
    /// parallel claims across paths, and the degenerate guards (non-positive power/activity).
    /// </summary>
    public class ProficiencyXpCalculatorTests
    {
        private const double Pie = 10.0;
        private const double MaxMultiplier = 4.0;

        [Fact]
        public void Split_ActivityEqualToPower_EarnsTheWholePie()
        {
            // activity == power → ratio 1 → the full pie.
            var slice = Assert.Single(Split(Pie, power: 100, MaxMultiplier, [new PathActivity(0, Activity: 100)]));
            Assert.Equal(0, slice.ProficiencyId);
            Assert.Equal(Pie, slice.Xp, precision: 9);
        }

        [Fact]
        public void Split_ActivityBelowPower_EarnsProportionally()
        {
            // A trivial fight: activity a quarter of power → a quarter of the pie (anti-grind).
            var slice = Assert.Single(Split(Pie, power: 100, MaxMultiplier, [new PathActivity(0, Activity: 25)]));
            Assert.Equal(Pie * 0.25, slice.Xp, precision: 9);
        }

        [Fact]
        public void Split_ActivityAbovePower_EarnsMore()
        {
            // Over-level / heavy-hitting: activity twice power → twice the pie (the continuous difficulty curve
            // that subsumes the banded difficulty multiplier).
            var slice = Assert.Single(Split(Pie, power: 100, MaxMultiplier, [new PathActivity(0, Activity: 200)]));
            Assert.Equal(Pie * 2.0, slice.Xp, precision: 9);
        }

        [Fact]
        public void Split_RatioFarAboveTheClamp_SaturatesAtMaxMultiplier()
        {
            // activity ÷ power far above the clamp saturates at maxMultiplier × pie — no unbounded single-battle
            // payout, mirroring ServerGameConstants.MaxExpRewardMultiplier.
            var slice = Assert.Single(Split(Pie, power: 1, MaxMultiplier, [new PathActivity(0, Activity: 1_000_000)]));
            Assert.Equal(Pie * MaxMultiplier, slice.Xp, precision: 9);
        }

        [Fact]
        public void Split_MultiplePaths_ClaimIndependently_NeedNotSumToOne()
        {
            // Two paths each with activity == power: each earns the FULL pie. There is no shared pie to dilute —
            // the claims overlap, so a multi-axis build mints more total proficiency XP than a single-axis one.
            var slices = Split(Pie, power: 100, MaxMultiplier,
                [new PathActivity(0, 100), new PathActivity(1, 100)]);

            Assert.Equal(Pie, slices.Single(s => s.ProficiencyId == 0).Xp, precision: 9);
            Assert.Equal(Pie, slices.Single(s => s.ProficiencyId == 1).Xp, precision: 9);
        }

        [Fact]
        public void Split_FocusBeatsSpread_ViaActivityShare()
        {
            // Focus vs spread lives in the activity the caller resolves per path: a focused path captures the
            // full damage (100), a dabbled one a fraction (25), so it trains at a quarter the rate.
            var slices = Split(Pie, power: 100, MaxMultiplier,
                [new PathActivity(0, 100), new PathActivity(1, 25)]);

            Assert.Equal(Pie, slices.Single(s => s.ProficiencyId == 0).Xp, precision: 9);
            Assert.Equal(Pie * 0.25, slices.Single(s => s.ProficiencyId == 1).Xp, precision: 9);
        }

        [Fact]
        public void Split_OmitsNonPositiveActivity()
        {
            // A path with zero activity earns nothing and is omitted (not a zero slice).
            var slices = Split(Pie, power: 100, MaxMultiplier,
                [new PathActivity(0, 100), new PathActivity(1, 0)]);

            Assert.Equal(0, Assert.Single(slices).ProficiencyId);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        public void Split_NonPositivePower_ReturnsEmpty(double power)
        {
            // A degenerate non-positive power (a real character always has positive locked-base attributes)
            // yields no slices rather than dividing by zero.
            Assert.Empty(Split(Pie, power, MaxMultiplier, [new PathActivity(0, 100)]));
        }

        [Fact]
        public void Split_NoActivities_ReturnsEmpty()
        {
            Assert.Empty(Split(Pie, power: 100, MaxMultiplier, []));
        }

        [Fact]
        public void Split_OrdersSlicesByProficiencyId()
        {
            var slices = Split(Pie, power: 100, MaxMultiplier,
                [new PathActivity(5, 100), new PathActivity(2, 100), new PathActivity(9, 100)]);

            Assert.Equal([2, 5, 9], slices.Select(s => s.ProficiencyId));
        }

        [Theory]
        [InlineData(50, Pie * 0.5)]
        [InlineData(100, Pie)]
        [InlineData(200, Pie * 2.0)]
        [InlineData(400, Pie * 4.0)] // exactly at the clamp
        public void Split_ScalesWithActivityOverPower(double activity, double expectedXp)
        {
            var slice = Assert.Single(Split(Pie, power: 100, MaxMultiplier, [new PathActivity(0, activity)]));
            Assert.Equal(expectedXp, slice.Xp, precision: 9);
        }
    }
}
