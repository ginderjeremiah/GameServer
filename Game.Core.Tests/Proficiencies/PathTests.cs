using Game.Core.Proficiencies;
using Xunit;
using CorePath = Game.Core.Proficiencies.Path;

namespace Game.Core.Tests.Proficiencies
{
    /// <summary>
    /// The path routing model (spike #982 decision 13): a represented path resolves to its frontier tier — the
    /// lowest un-maxed tier — and an off-tier skill's pull is discounted by <c>FalloffBase^distance</c>. These
    /// pin the frontier scan (it advances as tiers max and stops once the path is fully maxed) and the
    /// geometric falloff (full on-tier, decaying per tier of distance).
    /// </summary>
    public class PathTests
    {
        // A two-tier path (caps 10) with the given falloff base.
        private static CorePath TwoTierPath(double falloffBase) => new()
        {
            Id = 0,
            FalloffBase = falloffBase,
            Tiers = [new PathTier(ProficiencyId: 0, Ordinal: 0, MaxLevel: 10), new PathTier(1, 1, 10)],
        };

        [Fact]
        public void Frontier_UntrainedPath_IsTheFirstTier()
        {
            var frontier = TwoTierPath(0.3).Frontier(_ => 0);
            Assert.Equal(new PathTier(0, 0, 10), frontier);
        }

        [Fact]
        public void Frontier_FirstTierPartlyTrained_StaysOnTheFirstTier()
        {
            // Tier 0 below its cap is still the frontier — a tier opens only once the one before it is maxed.
            var frontier = TwoTierPath(0.3).Frontier(id => id == 0 ? 4 : 0);
            Assert.Equal(0, frontier?.ProficiencyId);
        }

        [Fact]
        public void Frontier_FirstTierMaxed_AdvancesToTheNextTier()
        {
            var frontier = TwoTierPath(0.3).Frontier(id => id == 0 ? 10 : 0);
            Assert.Equal(new PathTier(1, 1, 10), frontier);
        }

        [Fact]
        public void Frontier_EveryTierMaxed_IsNull()
        {
            // A fully-maxed path has no frontier — it banks nothing.
            Assert.Null(TwoTierPath(0.3).Frontier(_ => 10));
        }

        [Fact]
        public void Frontier_EmptyPath_IsNull()
        {
            var path = new CorePath { Id = 0, FalloffBase = 0.3, Tiers = [] };
            Assert.Null(path.Frontier(_ => 0));
        }

        [Theory]
        [InlineData(0, 1.0)]
        [InlineData(1, 0.3)]
        [InlineData(2, 0.09)]
        [InlineData(3, 0.027)]
        public void FalloffAt_IsGeometricInTheTierDistance(int distance, double expected)
        {
            Assert.Equal(expected, TwoTierPath(0.3).FalloffAt(distance), precision: 9);
        }

        [Fact]
        public void FalloffAt_NoFalloffBase_IsFlatOne()
        {
            var path = TwoTierPath(1.0);
            Assert.Equal(1.0, path.FalloffAt(0), precision: 9);
            Assert.Equal(1.0, path.FalloffAt(5), precision: 9);
        }
    }
}
