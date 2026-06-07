using Game.Core;
using Game.Core.Progress;
using Xunit;
using CorePlayerChallenge = Game.Core.Progress.PlayerChallenge;
using CorePlayerStatistic = Game.Core.Progress.PlayerStatistic;
using PlayerChallengeModel = Game.Api.Models.Progress.PlayerChallenge;
using PlayerStatisticModel = Game.Api.Models.Progress.PlayerStatistic;

namespace Game.Api.Tests.Unit
{
    public class ProgressModelMappingTests
    {
        [Fact]
        public void PlayerStatistic_FromSource_MapsDomainFields()
        {
            var source = new CorePlayerStatistic
            {
                Type = EStatisticType.EnemiesKilled,
                EntityId = 3,
                Value = 42m,
            };

            var model = PlayerStatisticModel.FromSource(source);

            Assert.Equal(EStatisticType.EnemiesKilled, model.StatisticTypeId);
            Assert.Equal(3, model.EntityId);
            Assert.Equal(42m, model.Value);
        }

        [Fact]
        public void PlayerStatistic_FromSource_PreservesNullEntityId()
        {
            var source = new CorePlayerStatistic
            {
                Type = EStatisticType.DamageDealt,
                EntityId = null,
                Value = 0m,
            };

            var model = PlayerStatisticModel.FromSource(source);

            Assert.Null(model.EntityId);
        }

        [Fact]
        public void PlayerChallenge_FromSource_MapsDomainFields()
        {
            var completedAt = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            var challenge = new Challenge
            {
                Id = 5,
                Name = "Slayer",
                Description = "Kill enemies",
                Type = new ChallengeType(EChallengeType.EnemiesKilled),
                ProgressGoal = 100m,
            };
            var source = new CorePlayerChallenge(challenge, progress: 100m, completed: true, completedAt);

            var model = PlayerChallengeModel.FromSource(source);

            Assert.Equal(5, model.ChallengeId);
            Assert.Equal(100m, model.Progress);
            Assert.True(model.Completed);
            Assert.Equal(completedAt, model.CompletedAt);
        }
    }
}
