using Game.Core;
using Game.Core.Progress;
using Xunit;

namespace Game.Core.Tests.Progress
{
    public class PlayerChallengeTests
    {
        private static readonly DateTime Timestamp = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void UpdateProgress_BelowGoal_StoresProgressAndStaysIncomplete()
        {
            var playerChallenge = MakePlayerChallenge(goal: 10);

            playerChallenge.UpdateProgress(4m, hasData: true, Timestamp);

            Assert.Equal(4m, playerChallenge.Progress);
            Assert.False(playerChallenge.Completed);
            Assert.Null(playerChallenge.CompletedAt);
        }

        [Fact]
        public void UpdateProgress_ReachesGoal_CompletesAndSetsTimestamp()
        {
            var playerChallenge = MakePlayerChallenge(goal: 10);

            playerChallenge.UpdateProgress(10m, hasData: true, Timestamp);

            Assert.Equal(10m, playerChallenge.Progress);
            Assert.True(playerChallenge.Completed);
            Assert.Equal(Timestamp, playerChallenge.CompletedAt);
        }

        [Fact]
        public void UpdateProgress_ExceedsGoal_ClampsProgressAndCompletes()
        {
            var playerChallenge = MakePlayerChallenge(goal: 10);

            playerChallenge.UpdateProgress(15m, hasData: true, Timestamp);

            Assert.Equal(10m, playerChallenge.Progress);
            Assert.True(playerChallenge.Completed);
        }

        [Fact]
        public void UpdateProgress_CalledAgainAfterCompletion_KeepsOriginalCompletedAt()
        {
            var playerChallenge = MakePlayerChallenge(goal: 10);

            playerChallenge.UpdateProgress(10m, hasData: true, Timestamp);
            playerChallenge.UpdateProgress(20m, hasData: true, Timestamp.AddDays(1));

            Assert.True(playerChallenge.Completed);
            Assert.Equal(Timestamp, playerChallenge.CompletedAt);
        }

        // ── "At most" goals (e.g. time trials) ───────────────────────────────

        [Fact]
        public void UpdateProgress_AtMost_ValueBelowGoal_Completes()
        {
            var playerChallenge = MakePlayerChallenge(goal: 10, type: EChallengeType.TimeTrial);

            playerChallenge.UpdateProgress(7m, hasData: true, Timestamp);

            Assert.Equal(7m, playerChallenge.Progress);
            Assert.True(playerChallenge.Completed);
            Assert.Equal(Timestamp, playerChallenge.CompletedAt);
        }

        [Fact]
        public void UpdateProgress_AtMost_ValueEqualsGoal_Completes()
        {
            var playerChallenge = MakePlayerChallenge(goal: 10, type: EChallengeType.TimeTrial);

            playerChallenge.UpdateProgress(10m, hasData: true, Timestamp);

            Assert.True(playerChallenge.Completed);
        }

        [Fact]
        public void UpdateProgress_AtMost_ValueAboveGoal_DoesNotComplete()
        {
            var playerChallenge = MakePlayerChallenge(goal: 10, type: EChallengeType.TimeTrial);

            playerChallenge.UpdateProgress(15m, hasData: true, Timestamp);

            Assert.Equal(15m, playerChallenge.Progress);
            Assert.False(playerChallenge.Completed);
            Assert.Null(playerChallenge.CompletedAt);
        }

        [Fact]
        public void UpdateProgress_AtMost_NoData_DoesNotComplete()
        {
            // With no recorded statistic the goal is unmet, even though the placeholder value is 0 and
            // 0 is at or below the goal — the absence of data, not the value, is what blocks completion.
            var playerChallenge = MakePlayerChallenge(goal: 10, type: EChallengeType.TimeTrial);

            playerChallenge.UpdateProgress(0m, hasData: false, Timestamp);

            Assert.False(playerChallenge.Completed);
            Assert.Null(playerChallenge.CompletedAt);
        }

        [Fact]
        public void UpdateProgress_AtMost_NoData_LeavesProgressUntouched()
        {
            // The 0 placeholder for an absent statistic must not be stored: for an "at most" goal a 0
            // reads as the best possible progress, so writing it would surface a misleading 0 best.
            var playerChallenge = MakePlayerChallenge(goal: 10, type: EChallengeType.TimeTrial, initialProgress: 42m);

            playerChallenge.UpdateProgress(0m, hasData: false, Timestamp);

            Assert.Equal(42m, playerChallenge.Progress);
        }

        [Fact]
        public void UpdateProgress_AtMost_GenuineZeroValue_Completes()
        {
            // A recorded 0 (e.g. an instant victory) is a legitimate value at or below the goal, so it
            // must complete — the old "0 means no data" sentinel made this case unwinnable.
            var playerChallenge = MakePlayerChallenge(goal: 10, type: EChallengeType.TimeTrial);

            playerChallenge.UpdateProgress(0m, hasData: true, Timestamp);

            Assert.True(playerChallenge.Completed);
            Assert.Equal(Timestamp, playerChallenge.CompletedAt);
        }

        private static PlayerChallenge MakePlayerChallenge(decimal goal, EChallengeType type = EChallengeType.EnemiesKilled, decimal initialProgress = 0m)
        {
            var challenge = new Challenge
            {
                Id = 0,
                Name = "Test Challenge",
                Description = string.Empty,
                DesignerNotes = string.Empty,
                Type = new ChallengeType(type),
                ProgressGoal = goal,
            };

            return new PlayerChallenge(challenge, initialProgress, completed: false);
        }
    }
}
