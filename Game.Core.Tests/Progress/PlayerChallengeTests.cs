using Game.Core;
using Game.Core.Challenges;
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

        private static PlayerChallenge MakePlayerChallenge(decimal goal)
        {
            var challenge = new Challenge
            {
                Id = 0,
                Name = "Test Challenge",
                Description = string.Empty,
                Type = new ChallengeType(EChallengeType.EnemiesKilled),
                ProgressGoal = goal,
            };

            return new PlayerChallenge(challenge, progress: 0m, completed: false);
        }
    }
}
