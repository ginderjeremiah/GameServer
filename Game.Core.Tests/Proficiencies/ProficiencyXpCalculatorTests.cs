using Game.Core.Proficiencies;
using Xunit;
using static Game.Core.Proficiencies.ProficiencyXpCalculator;

namespace Game.Core.Tests.Proficiencies
{
    /// <summary>
    /// The fixed-pie split (spike #982 decision 4): a constant pie, scaled by the battle's difficulty
    /// multiplier, divided across represented proficiencies in proportion to their summed contribution
    /// weights. These pin the two documented consequences — diversifying dilutes, and a fast skill earns no
    /// more than a slow one (the caller already collapses uses to representation) — plus the scaling and the
    /// empty-result edges.
    /// </summary>
    public class ProficiencyXpCalculatorTests
    {
        private const double Pie = 10.0;

        [Fact]
        public void Split_SingleRepresentedProficiency_TakesTheWholePie()
        {
            var slices = Split(Pie, difficultyMultiplier: 1.0, [new WeightedContribution(0, 1.0)]);

            var slice = Assert.Single(slices);
            Assert.Equal(0, slice.ProficiencyId);
            Assert.Equal(Pie, slice.Xp, precision: 9);
        }

        [Fact]
        public void Split_FourSkillsOneProficiency_TrainsItAtFullRate()
        {
            // A loadout of four skills all feeding one proficiency: the proficiency's weight is the sum (4),
            // it is the only one represented, so it still takes the whole pie — full rate.
            var contributions = Enumerable.Repeat(new WeightedContribution(0, 1.0), 4);

            var slice = Assert.Single(Split(Pie, 1.0, contributions));
            Assert.Equal(Pie, slice.Xp, precision: 9);
        }

        [Fact]
        public void Split_FourSkillsAcrossFourProficiencies_TrainsEachAtAQuarterRate()
        {
            // The dilution property: spreading the same four skills across four proficiencies gives each a
            // quarter of the pie rather than the full rate a focused loadout earns.
            var contributions = Enumerable.Range(0, 4).Select(id => new WeightedContribution(id, 1.0));

            var slices = Split(Pie, 1.0, contributions);

            Assert.Equal(4, slices.Count);
            Assert.All(slices, slice => Assert.Equal(Pie / 4, slice.Xp, precision: 9));
        }

        [Fact]
        public void Split_UnequalWeights_SplitsProportionally()
        {
            var slices = Split(Pie, 1.0, [new WeightedContribution(0, 3.0), new WeightedContribution(1, 1.0)]);

            Assert.Equal(Pie * 3 / 4, slices.Single(s => s.ProficiencyId == 0).Xp, precision: 9);
            Assert.Equal(Pie * 1 / 4, slices.Single(s => s.ProficiencyId == 1).Xp, precision: 9);
        }

        [Fact]
        public void Split_MultiContributionSkill_SumsWeightsPerProficiency()
        {
            // One skill feeding two proficiencies plus a second skill reinforcing the first: proficiency 0's
            // weight is 1 + 2 = 3, proficiency 1's is 1, so 0 takes three-quarters.
            var contributions = new[]
            {
                new WeightedContribution(0, 1.0),
                new WeightedContribution(1, 1.0),
                new WeightedContribution(0, 2.0),
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
            // The whole pie goes to the single represented proficiency, so its slice is exactly pie × multiplier
            // — a trivial enemy (small multiplier) pays little, an over-level one pays more (anti-grind).
            var slice = Assert.Single(Split(Pie, multiplier, [new WeightedContribution(0, 1.0)]));
            Assert.Equal(expectedXp, slice.Xp, precision: 9);
        }

        [Fact]
        public void Split_NoContributions_ReturnsEmpty()
        {
            Assert.Empty(Split(Pie, 1.0, []));
        }

        [Fact]
        public void Split_NonPositiveTotalWeight_ReturnsEmpty()
        {
            // A degenerate zero-weight contribution represents nothing trainable.
            Assert.Empty(Split(Pie, 1.0, [new WeightedContribution(0, 0.0)]));
        }

        [Fact]
        public void Split_OrdersSlicesByProficiencyId()
        {
            var contributions = new[]
            {
                new WeightedContribution(5, 1.0),
                new WeightedContribution(2, 1.0),
                new WeightedContribution(9, 1.0),
            };

            var ids = Split(Pie, 1.0, contributions).Select(s => s.ProficiencyId);
            Assert.Equal([2, 5, 9], ids);
        }
    }
}
