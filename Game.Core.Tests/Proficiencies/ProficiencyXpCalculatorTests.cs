using Game.Core.Proficiencies;
using Xunit;
using static Game.Core.Proficiencies.ProficiencyXpCalculator;

namespace Game.Core.Tests.Proficiencies
{
    /// <summary>
    /// The absolute-falloff split (spike #982 decision 13): a constant pie, scaled by the battle's difficulty
    /// multiplier, divided across represented paths' frontier tiers by their falloff-free <em>attention</em>,
    /// then each slice scaled by its on-tier <em>efficiency</em> (the attention-weighted average falloff). The
    /// pie is a ceiling, not a constant — the un-earned remainder evaporates rather than being redistributed.
    /// These pin the documented consequences: the absolute (not merely relative) slowdown of a solo coasting
    /// path, the full pace of a fresh tier trained on its native seed, attention × efficiency across multiple
    /// paths, and the dilution/representation/scaling edges carried over from the fixed-pie split.
    /// </summary>
    public class ProficiencyXpCalculatorTests
    {
        private const double Pie = 10.0;

        // A skill firing on its own tier: full attention, no falloff.
        private static WeightedContribution OnTier(int proficiencyId, double attention = 1.0) =>
            new(proficiencyId, attention, Falloff: 1.0);

        [Fact]
        public void Split_SoloPath_OnTierLoadout_TakesTheWholePie()
        {
            // A path trained on a skill native to its frontier tier (falloff 1) is the only one represented, so
            // it earns the whole pie — full pace.
            var slice = Assert.Single(Split(Pie, difficultyMultiplier: 1.0, [OnTier(0)]));
            Assert.Equal(0, slice.ProficiencyId);
            Assert.Equal(Pie, slice.Xp, precision: 9);
        }

        [Fact]
        public void Split_SoloPath_CoastingOnStaleSkills_SlowsInAbsoluteTerms_AndEvaporatesTheRest()
        {
            // The headline change: a solo path coasting one tier behind (falloff 0.3) earns 0.3 × the pie, not
            // the whole pie. Because it is the only path, a relative split would still hand it everything; the
            // absolute model mints only the earned 0.3 and the remaining 0.7 evaporates (is not minted).
            var slice = Assert.Single(Split(Pie, 1.0, [new WeightedContribution(0, Attention: 1.0, Falloff: 0.3)]));
            Assert.Equal(Pie * 0.3, slice.Xp, precision: 9);
        }

        [Fact]
        public void Split_FreshTierOnItsNativeSeed_TrainsAtFullPace()
        {
            // A freshly-opened tier trained on its native seed skill (home tier = the tier, falloff 1) trains at
            // full pace — never walled by being deep, the pull to fill the loadout with the new tier's natives.
            var slice = Assert.Single(Split(Pie, 1.0, [OnTier(3)]));
            Assert.Equal(3, slice.ProficiencyId);
            Assert.Equal(Pie, slice.Xp, precision: 9);
        }

        [Fact]
        public void Split_FreshTier_SeedAtFullPlusInheritedAtDiscount_EarnsTheWeightedAverageEfficiency()
        {
            // The same fresh tier trained on its native seed (falloff 1, attention 1) plus an inherited
            // lower-tier skill supplementing it (falloff 0.3, attention 1): the tier's on-tier efficiency is the
            // attention-weighted average falloff (1 + 0.3) / 2 = 0.65, so it earns 0.65 × the pie.
            var contributions = new[]
            {
                new WeightedContribution(3, Attention: 1.0, Falloff: 1.0),
                new WeightedContribution(3, Attention: 1.0, Falloff: 0.3),
            };

            var slice = Assert.Single(Split(Pie, 1.0, contributions));
            Assert.Equal(Pie * 0.65, slice.Xp, precision: 9);
        }

        [Fact]
        public void Split_MultiPath_SplitsByAttentionThenScalesEachByItsOwnEfficiency()
        {
            // Two paths with equal attention (2 each): path 0 on-tier (efficiency 1), path 1 coasting
            // (efficiency 0.3). Each claims half the pie by attention; path 0 mints its full half (5), path 1
            // mints 0.3 of its half (1.5). The remainder evaporates — path 1's staleness does not subsidize
            // path 0, so the slowdown is absolute, not relative.
            var contributions = new[]
            {
                new WeightedContribution(0, Attention: 2.0, Falloff: 1.0),
                new WeightedContribution(1, Attention: 2.0, Falloff: 0.3),
            };

            var slices = Split(Pie, 1.0, contributions);

            Assert.Equal(Pie / 2, slices.Single(s => s.ProficiencyId == 0).Xp, precision: 9);
            Assert.Equal(Pie / 2 * 0.3, slices.Single(s => s.ProficiencyId == 1).Xp, precision: 9);
        }

        [Fact]
        public void Split_FourSkillsOnePath_TrainsItAtFullRate()
        {
            // Four on-tier skills all routing to one frontier tier: its attention is the sum (4), it is the only
            // tier represented, so it still earns the whole pie — focus is full rate.
            var contributions = Enumerable.Repeat(OnTier(0), 4);

            var slice = Assert.Single(Split(Pie, 1.0, contributions));
            Assert.Equal(Pie, slice.Xp, precision: 9);
        }

        [Fact]
        public void Split_FourSkillsAcrossFourPaths_TrainsEachAtAQuarterRate()
        {
            // The dilution property: four on-tier skills spread across four paths give each a quarter of the
            // pie (equal attention, full efficiency) rather than the full rate a focused loadout earns.
            var contributions = Enumerable.Range(0, 4).Select(id => OnTier(id));

            var slices = Split(Pie, 1.0, contributions);

            Assert.Equal(4, slices.Count);
            Assert.All(slices, slice => Assert.Equal(Pie / 4, slice.Xp, precision: 9));
        }

        [Fact]
        public void Split_UnequalAttention_SplitsProportionally()
        {
            var slices = Split(Pie, 1.0, [OnTier(0, attention: 3.0), OnTier(1, attention: 1.0)]);

            Assert.Equal(Pie * 3 / 4, slices.Single(s => s.ProficiencyId == 0).Xp, precision: 9);
            Assert.Equal(Pie * 1 / 4, slices.Single(s => s.ProficiencyId == 1).Xp, precision: 9);
        }

        [Fact]
        public void Split_MultipleContributionsToOneTier_SumAttention()
        {
            // One skill feeding two paths plus a second skill reinforcing the first frontier tier: tier 0's
            // attention is 1 + 2 = 3, tier 1's is 1 (all on-tier), so 0 earns three-quarters.
            var contributions = new[]
            {
                OnTier(0, attention: 1.0),
                OnTier(1, attention: 1.0),
                OnTier(0, attention: 2.0),
            };

            var slices = Split(Pie, 1.0, contributions);

            Assert.Equal(Pie * 3 / 4, slices.Single(s => s.ProficiencyId == 0).Xp, precision: 9);
            Assert.Equal(Pie * 1 / 4, slices.Single(s => s.ProficiencyId == 1).Xp, precision: 9);
        }

        [Theory]
        [InlineData(0.0, 0.0)]
        [InlineData(0.25, Pie * 0.25)]
        [InlineData(1.0, Pie)]
        [InlineData(2.0, Pie * 2.0)]
        public void Split_ScalesTheTotalByTheDifficultyMultiplier(double multiplier, double expectedXp)
        {
            // The whole pie goes to the single on-tier path, so its slice is exactly pie × multiplier — a
            // trivial enemy (small multiplier) pays little, an over-level one pays more (anti-grind).
            var slice = Assert.Single(Split(Pie, multiplier, [OnTier(0)]));
            Assert.Equal(expectedXp, slice.Xp, precision: 9);
        }

        [Fact]
        public void Split_FullyEvaporatedContribution_YieldsAZeroSlice()
        {
            // A represented path with zero efficiency (falloff base 0, one tier behind) earns nothing: the
            // attention is real (so the path is represented) but the whole slice evaporates. The accrual's
            // rounding guard drops the resulting zero rather than persisting it.
            var slice = Assert.Single(Split(Pie, 1.0, [new WeightedContribution(0, Attention: 1.0, Falloff: 0.0)]));
            Assert.Equal(0.0, slice.Xp, precision: 9);
        }

        [Fact]
        public void Split_NoContributions_ReturnsEmpty()
        {
            Assert.Empty(Split(Pie, 1.0, []));
        }

        [Fact]
        public void Split_NonPositiveTotalAttention_ReturnsEmpty()
        {
            // A degenerate zero-attention contribution represents nothing trainable.
            Assert.Empty(Split(Pie, 1.0, [OnTier(0, attention: 0.0)]));
        }

        [Fact]
        public void Split_ZeroAttentionTierAlongsidePositive_DegradesToZero_NotNaN()
        {
            // A zero-attention tier (authored Weight 0, or a 0 tier weight once #979 lands) mixed with a
            // positive one: the total attention is positive so it is not filtered out, and its slice must fall
            // out as 0 rather than 0/0 = NaN — a NaN would throw on the downstream (decimal)Xp cast.
            var contributions = new[] { OnTier(0, attention: 1.0), new WeightedContribution(1, Attention: 0.0, Falloff: 1.0) };

            var slices = Split(Pie, 1.0, contributions);

            Assert.Equal(Pie, slices.Single(s => s.ProficiencyId == 0).Xp, precision: 9);
            var zero = slices.Single(s => s.ProficiencyId == 1).Xp;
            Assert.False(double.IsNaN(zero));
            Assert.Equal(0.0, zero, precision: 9);
        }

        [Fact]
        public void Split_OrdersSlicesByProficiencyId()
        {
            var contributions = new[] { OnTier(5), OnTier(2), OnTier(9) };

            var ids = Split(Pie, 1.0, contributions).Select(s => s.ProficiencyId);
            Assert.Equal([2, 5, 9], ids);
        }
    }
}
