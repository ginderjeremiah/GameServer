using Game.Core;
using Game.Core.Progress;
using Xunit;

namespace Game.Core.Tests.Progress
{
    public class ChallengeIndexTests
    {
        [Fact]
        public void RelevantTo_TouchedStatistic_ReturnsChallengesKeyedToIt()
        {
            var killChallenge = MakeChallenge(id: 0, EChallengeType.EnemiesKilled);
            var damageChallenge = MakeChallenge(id: 1, EChallengeType.DamageDealt);
            var index = new ChallengeIndex([killChallenge, damageChallenge]);

            var relevant = index.RelevantTo([(EStatisticType.EnemiesKilled, null)]).ToList();

            // Only the challenge tracking the touched statistic is returned; the unrelated one is excluded.
            Assert.Contains(killChallenge, relevant);
            Assert.DoesNotContain(damageChallenge, relevant);
        }

        [Fact]
        public void RelevantTo_PerEntityChallenge_MatchesOnTheExactEntity()
        {
            var enemyThree = MakeChallenge(id: 0, EChallengeType.EnemiesKilled, targetEntityId: 3);
            var enemyFive = MakeChallenge(id: 1, EChallengeType.EnemiesKilled, targetEntityId: 5);
            var index = new ChallengeIndex([enemyThree, enemyFive]);

            // A battle against enemy 3 touches that enemy's per-entity row (and the global total).
            var relevant = index.RelevantTo(
            [
                (EStatisticType.EnemiesKilled, null),
                (EStatisticType.EnemiesKilled, 3),
            ]).ToList();

            // The per-entity precision: enemy 5's challenge is not evaluated for a battle against enemy 3,
            // even though both target the same statistic type.
            Assert.Contains(enemyThree, relevant);
            Assert.DoesNotContain(enemyFive, relevant);
        }

        [Fact]
        public void RelevantTo_StatisticIndependentChallenges_AreAlwaysReturned()
        {
            // LevelReached reads the player's level directly, not a recorded statistic, so it is relevant to
            // every completed battle (a victory can level the player up) regardless of which statistics moved.
            var levelChallenge = MakeChallenge(id: 0, EChallengeType.LevelReached);
            var killChallenge = MakeChallenge(id: 1, EChallengeType.EnemiesKilled);
            var index = new ChallengeIndex([levelChallenge, killChallenge]);

            // Touch a statistic unrelated to either challenge's stat key.
            var relevant = index.RelevantTo([(EStatisticType.DamageTaken, null)]).ToList();

            Assert.Contains(levelChallenge, relevant);
            Assert.DoesNotContain(killChallenge, relevant);
        }

        [Fact]
        public void RelevantTo_NoTouchedStatistics_ReturnsOnlyStatisticIndependentChallenges()
        {
            var levelChallenge = MakeChallenge(id: 0, EChallengeType.LevelReached);
            var killChallenge = MakeChallenge(id: 1, EChallengeType.EnemiesKilled);
            var index = new ChallengeIndex([levelChallenge, killChallenge]);

            var relevant = index.RelevantTo([]).ToList();

            Assert.Equal([levelChallenge], relevant);
        }

        [Fact]
        public void RelevantTo_EachChallengeYieldedAtMostOnce()
        {
            var global = MakeChallenge(id: 0, EChallengeType.EnemiesKilled);
            var perEnemy = MakeChallenge(id: 1, EChallengeType.EnemiesKilled, targetEntityId: 3);
            var level = MakeChallenge(id: 2, EChallengeType.LevelReached);
            var index = new ChallengeIndex([global, perEnemy, level]);

            var relevant = index.RelevantTo(
            [
                (EStatisticType.EnemiesKilled, null),
                (EStatisticType.EnemiesKilled, 3),
            ]).ToList();

            Assert.Equal(3, relevant.Count);
            Assert.Equal(relevant.Count, relevant.Distinct().Count());
        }

        private static Challenge MakeChallenge(int id, EChallengeType type, int? targetEntityId = null) => new()
        {
            Id = id,
            Name = "Test Challenge",
            Description = string.Empty,
            Type = new ChallengeType(type),
            TargetEntityId = targetEntityId,
            ProgressGoal = 1m,
        };
    }
}
