using Game.Core;
using Game.Core.Progress;
using Xunit;

namespace Game.Core.Tests.Progress
{
    public class PlayerChallengeTests
    {
        [Fact]
        public void UpdateProgress_BelowGoal_StoresProgressAndStaysIncomplete()
        {
            var playerChallenge = MakePlayerChallenge(goal: 10);

            playerChallenge.UpdateProgress(4m);

            Assert.Equal(4m, playerChallenge.Progress);
            Assert.False(playerChallenge.Completed);
            Assert.Null(playerChallenge.CompletedAt);
        }

        [Fact]
        public void UpdateProgress_ReachesGoal_CompletesAndSetsTimestamp()
        {
            var playerChallenge = MakePlayerChallenge(goal: 10);

            playerChallenge.UpdateProgress(10m);

            Assert.Equal(10m, playerChallenge.Progress);
            Assert.True(playerChallenge.Completed);
            Assert.NotNull(playerChallenge.CompletedAt);
        }

        [Fact]
        public void UpdateProgress_ExceedsGoal_ClampsProgressAndCompletes()
        {
            var playerChallenge = MakePlayerChallenge(goal: 10);

            playerChallenge.UpdateProgress(15m);

            Assert.Equal(10m, playerChallenge.Progress);
            Assert.True(playerChallenge.Completed);
        }

        [Fact]
        public void UpdateProgress_CalledAgainAfterCompletion_KeepsOriginalCompletedAt()
        {
            var playerChallenge = MakePlayerChallenge(goal: 10);

            playerChallenge.UpdateProgress(10m);
            var firstCompletedAt = playerChallenge.CompletedAt;

            playerChallenge.UpdateProgress(20m);

            Assert.True(playerChallenge.Completed);
            Assert.Equal(firstCompletedAt, playerChallenge.CompletedAt);
        }

        // ── "At most" goals (e.g. time trials) ───────────────────────────────

        [Fact]
        public void UpdateProgress_AtMost_ValueBelowGoal_Completes()
        {
            var playerChallenge = MakePlayerChallenge(goal: 10, type: EChallengeType.TimeTrial);

            playerChallenge.UpdateProgress(7m);

            Assert.Equal(7m, playerChallenge.Progress);
            Assert.True(playerChallenge.Completed);
            Assert.NotNull(playerChallenge.CompletedAt);
        }

        [Fact]
        public void UpdateProgress_AtMost_ValueEqualsGoal_Completes()
        {
            var playerChallenge = MakePlayerChallenge(goal: 10, type: EChallengeType.TimeTrial);

            playerChallenge.UpdateProgress(10m);

            Assert.True(playerChallenge.Completed);
        }

        [Fact]
        public void UpdateProgress_AtMost_ValueAboveGoal_DoesNotComplete()
        {
            var playerChallenge = MakePlayerChallenge(goal: 10, type: EChallengeType.TimeTrial);

            playerChallenge.UpdateProgress(15m);

            Assert.Equal(15m, playerChallenge.Progress);
            Assert.False(playerChallenge.Completed);
            Assert.Null(playerChallenge.CompletedAt);
        }

        [Fact]
        public void UpdateProgress_AtMost_ZeroValue_TreatedAsNoDataAndDoesNotComplete()
        {
            // 0 means the underlying statistic (e.g. fastest victory) has no data yet,
            // so it must not satisfy a "win within N seconds" goal.
            var playerChallenge = MakePlayerChallenge(goal: 10, type: EChallengeType.TimeTrial);

            playerChallenge.UpdateProgress(0m);

            Assert.False(playerChallenge.Completed);
        }

        private static PlayerChallenge MakePlayerChallenge(decimal goal, EChallengeType type = EChallengeType.EnemiesKilled)
        {
            var challenge = new Challenge
            {
                Id = 0,
                Name = "Test Challenge",
                Description = string.Empty,
                Type = new ChallengeType(type),
                ProgressGoal = goal,
            };

            return new PlayerChallenge(challenge, progress: 0m, completed: false);
        }
    }
}
