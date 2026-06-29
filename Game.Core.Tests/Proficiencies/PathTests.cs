using Game.Core;
using Game.Core.Proficiencies;
using Xunit;
using CorePath = Game.Core.Proficiencies.Path;

namespace Game.Core.Tests.Proficiencies
{
    /// <summary>
    /// The path routing model (spike #1318): a path declares one activity key and resolves to its frontier
    /// tier — the lowest un-maxed tier — which the effect-based accrual claims XP for. These pin the frontier
    /// scan (it advances as tiers max and stops once the path is fully maxed) and the successor lookup.
    /// </summary>
    public class PathTests
    {
        // A two-tier path (caps 10) keyed on the given activity.
        private static CorePath TwoTierPath(EActivityKey activityKey = EActivityKey.Physical) => new()
        {
            Id = 0,
            ActivityKey = activityKey,
            Tiers = [new PathTier(ProficiencyId: 0, Ordinal: 0, MaxLevel: 10), new PathTier(1, 1, 10)],
        };

        [Fact]
        public void Frontier_UntrainedPath_IsTheFirstTier()
        {
            var frontier = TwoTierPath().Frontier(_ => 0);
            Assert.Equal(new PathTier(0, 0, 10), frontier);
        }

        [Fact]
        public void Frontier_FirstTierPartlyTrained_StaysOnTheFirstTier()
        {
            // Tier 0 below its cap is still the frontier — a tier opens only once the one before it is maxed.
            var frontier = TwoTierPath().Frontier(id => id == 0 ? 4 : 0);
            Assert.Equal(0, frontier?.ProficiencyId);
        }

        [Fact]
        public void Frontier_FirstTierMaxed_AdvancesToTheNextTier()
        {
            var frontier = TwoTierPath().Frontier(id => id == 0 ? 10 : 0);
            Assert.Equal(new PathTier(1, 1, 10), frontier);
        }

        [Fact]
        public void Frontier_EveryTierMaxed_IsNull()
        {
            // A fully-maxed path has no frontier — it banks nothing.
            Assert.Null(TwoTierPath().Frontier(_ => 10));
        }

        [Fact]
        public void Frontier_EmptyPath_IsNull()
        {
            var path = new CorePath { Id = 0, ActivityKey = EActivityKey.Physical, Tiers = [] };
            Assert.Null(path.Frontier(_ => 0));
        }

        [Fact]
        public void NextTier_ReturnsTheSuccessorTier()
        {
            Assert.Equal(new PathTier(1, 1, 10), TwoTierPath().NextTier(0));
        }

        [Fact]
        public void NextTier_OfTheLastTier_IsNull()
        {
            Assert.Null(TwoTierPath().NextTier(1));
        }
    }
}
