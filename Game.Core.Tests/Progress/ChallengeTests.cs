using Game.Core.Players;
using Game.Core.Progress;
using Game.Core.TestInfrastructure.Builders;
using Xunit;

namespace Game.Core.Tests.Progress
{
    public class ChallengeTests
    {
        [Fact]
        public void UpdateChallengeProgress_StatisticType_UsesMatchingStatisticValue()
        {
            var challenge = MakeChallenge(EChallengeType.EnemiesKilled, goal: 10);
            var playerChallenge = new PlayerChallenge(challenge, progress: 0m, completed: false);
            var progress = MakeProgress(statistics:
            [
                new PlayerStatistic { Type = EStatisticType.EnemiesKilled, EntityId = null, Value = 7m },
            ]);

            challenge.UpdateChallengeProgress(playerChallenge, progress);

            Assert.Equal(7m, playerChallenge.Progress);
            Assert.False(playerChallenge.Completed);
        }

        [Fact]
        public void UpdateChallengeProgress_StatisticType_RespectsTargetEntityId()
        {
            var challenge = MakeChallenge(EChallengeType.EnemiesKilled, goal: 5, targetEntityId: 4);
            var playerChallenge = new PlayerChallenge(challenge, progress: 0m, completed: false);
            var progress = MakeProgress(statistics:
            [
                new PlayerStatistic { Type = EStatisticType.EnemiesKilled, EntityId = null, Value = 50m },
                new PlayerStatistic { Type = EStatisticType.EnemiesKilled, EntityId = 4, Value = 5m },
            ]);

            challenge.UpdateChallengeProgress(playerChallenge, progress);

            Assert.Equal(5m, playerChallenge.Progress);
            Assert.True(playerChallenge.Completed);
        }

        [Fact]
        public void UpdateChallengeProgress_LevelReached_UsesPlayerLevel()
        {
            var challenge = MakeChallenge(EChallengeType.LevelReached, goal: 8);
            var playerChallenge = new PlayerChallenge(challenge, progress: 0m, completed: false);
            var progress = MakeProgress(player: new PlayerBuilder().WithLevel(8).Build());

            challenge.UpdateChallengeProgress(playerChallenge, progress);

            Assert.Equal(8m, playerChallenge.Progress);
            Assert.True(playerChallenge.Completed);
        }

        [Fact]
        public void UpdateChallengeProgress_StatisticTypeWithNoData_LeavesProgressAtZero()
        {
            var challenge = MakeChallenge(EChallengeType.DamageDealt, goal: 100);
            var playerChallenge = new PlayerChallenge(challenge, progress: 0m, completed: false);
            var progress = MakeProgress();

            challenge.UpdateChallengeProgress(playerChallenge, progress);

            Assert.Equal(0m, playerChallenge.Progress);
            Assert.False(playerChallenge.Completed);
        }

        [Fact]
        public void UpdateChallengeProgress_EveryChallengeType_AdvancesProgress_NeverSilentlyNoOps()
        {
            // Each challenge type must actually move progress when its tracked value is satisfied — a
            // statistic-backed type from its statistic, LevelReached from the player's level. A type that
            // is neither would fall through the fail-loud guard and throw, so this fails on a half-wired
            // new type rather than letting it silently never progress.
            var player = new PlayerBuilder().WithLevel(50).Build();

            foreach (var type in Enum.GetValues<EChallengeType>())
            {
                var challenge = MakeChallenge(type, goal: 1);
                var playerChallenge = new PlayerChallenge(challenge, progress: 0m, completed: false);
                var statisticType = challenge.Type.StatisticType;
                var statistics = statisticType is not null
                    ? new[] { new PlayerStatistic { Type = statisticType.Id, EntityId = null, Value = 1m } }
                    : [];
                var progress = MakeProgress(player, statistics);

                challenge.UpdateChallengeProgress(playerChallenge, progress);

                Assert.True(playerChallenge.Completed, $"Challenge type {type} did not progress.");
            }
        }

        private static Challenge MakeChallenge(EChallengeType type, decimal goal, int? targetEntityId = null) => new()
        {
            Id = 0,
            Name = "Test Challenge",
            Description = string.Empty,
            DesignerNotes = string.Empty,
            Type = new ChallengeType(type),
            TargetEntityId = targetEntityId,
            ProgressGoal = goal,
        };

        private static PlayerProgress MakeProgress(Player? player = null, IEnumerable<PlayerStatistic>? statistics = null) =>
            new(player ?? new PlayerBuilder().Build(), statistics ?? [], [], []);
    }
}
