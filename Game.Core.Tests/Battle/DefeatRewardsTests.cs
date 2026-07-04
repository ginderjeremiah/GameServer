using Game.Core;
using Game.Core.Battle;
using Xunit;

namespace Game.Core.Tests.Battle
{
    /// <summary>
    /// The enemy-authored bounty curve (spike #1526 Decision 4): <c>XP/kill = k × enemyRating × min(r, 1)²</c>,
    /// <c>r = enemyRating ÷ playerRating</c>. Unlike the retired ±20%-band/quadratic-premium curve, the
    /// multiplier never exceeds <c>1</c> — punching up saturates the bounty rather than jackpotting it — so
    /// these tests exercise ratings directly rather than the retired core-attribute-sum inputs.
    /// </summary>
    public class DefeatRewardsTests
    {
        private const double K = ServerGameConstants.CombatRatingXpScale;

        [Fact]
        public void ExpReward_MatchedFight_MultiplierIsOne_ExpEqualsKTimesEnemyRating()
        {
            var rewards = new DefeatRewards(playerRating: 100, enemyRating: 100);

            Assert.Equal(1.0, rewards.DifficultyMultiplier, precision: 9);
            Assert.Equal((int)Math.Floor(K * 100), rewards.ExpReward);
        }

        [Fact]
        public void ExpReward_EnemyFarBelowPlayer_SmallRatio_SmallMultiplier_AntiGrind()
        {
            // r = 10/100 = 0.1 → multiplier 0.01 — a trivial enemy pays almost nothing.
            var rewards = new DefeatRewards(playerRating: 100, enemyRating: 10);

            Assert.Equal(0.01, rewards.DifficultyMultiplier, precision: 9);
            Assert.Equal((int)Math.Floor(K * 10 * 0.01), rewards.ExpReward);
        }

        [Fact]
        public void ExpReward_EnemyFarAbovePlayer_RatioClampsAtOne_NoUnboundedPayout()
        {
            // r = 1000/10 = 100 ≫ 1 → clamped to 1, so the multiplier saturates at 1 rather than scaling to
            // 100² = 10000 — punching up pays the bigger bounty (enemyRating) at a saturated, not unbounded,
            // multiplier.
            var rewards = new DefeatRewards(playerRating: 10, enemyRating: 1000);

            Assert.Equal(1.0, rewards.DifficultyMultiplier, precision: 9);
            Assert.Equal((int)Math.Floor(K * 1000), rewards.ExpReward);
        }

        [Theory]
        [InlineData(100, 100, 1.0)]  // r = 1.0 (matched)
        [InlineData(10, 100, 0.01)]  // r = 0.1 → 0.01 (anti-grind)
        [InlineData(150, 100, 1.0)]  // r = 1.5 → clamped to 1 (no upward premium)
        [InlineData(1000, 10, 1.0)]  // r = 100 → clamped to 1
        public void DifficultyMultiplier_IsMinRatioSquaredNeverExceedingOne(
            double enemyRating, double playerRating, double expected)
        {
            var rewards = new DefeatRewards(playerRating, enemyRating);

            Assert.Equal(expected, rewards.DifficultyMultiplier, precision: 9);
        }

        [Fact]
        public void ExpReward_EnemyRatingAboveIntRange_ClampsToIntMaxValueInsteadOfWrapping()
        {
            // r ≫ 1 → multiplier clamps to 1, but enemyRating itself is unbounded (a large authored enemy can
            // still push k × enemyRating past int.MaxValue); the unclamped (int) cast would wrap to a negative
            // value that GrantExp floors to 0, silently zeroing a legitimate reward.
            var rewards = new DefeatRewards(playerRating: 1, enemyRating: 1e13);

            Assert.Equal(int.MaxValue, rewards.ExpReward);
        }

        [Fact]
        public void PlayerRating_EqualsThePassedInValue()
        {
            var rewards = new DefeatRewards(playerRating: 42.5, enemyRating: 10);

            Assert.Equal(42.5, rewards.PlayerRating, precision: 9);
        }
    }
}
