using Game.Abstractions.DataAccess;
using Game.Application;
using Game.Core;
using Game.Core.Battle;
using Game.Core.Enemies;
using Game.Core.Players;
using Game.Core.Players.Inventories;
using Game.Core.Progress;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Base;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Game.Application.Tests.DataAccess
{
    /// <summary>
    /// Verifies <see cref="IPlayerProgressRepository"/> persists statistics and challenge progress.
    /// In particular, <see cref="IPlayerProgressRepository.Save"/> no longer depends on a prior
    /// <see cref="IPlayerProgressRepository.Load"/> on the same instance (the temporal coupling removed
    /// in #164): it fetches the existing snapshot on demand, so a standalone save upserts correctly.
    /// </summary>
    [Collection("Integration")]
    public class PlayerProgressRepositoryIntegrationTests : ApplicationIntegrationTestBase
    {
        public PlayerProgressRepositoryIntegrationTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

        [Fact]
        public async Task Save_WithoutPriorLoad_InsertsNewStatistics()
        {
            var playerId = await SeedPlayerAsync();

            using (var scope = CreateScope())
            {
                var repo = scope.ServiceProvider.GetRequiredService<IPlayerProgressRepository>();
                var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                var progress = new PlayerProgress(
                    MakeDomainPlayer(playerId),
                    [new PlayerStatistic { Type = EStatisticType.EnemiesKilled, EntityId = null, Value = 7m }],
                    []);

                // Save is called without ever calling Load — this previously threw NullReferenceException.
                await repo.Save(progress);
                await unitOfWork.CommitAsync();
            }

            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                var stat = await context.PlayerStatistics.AsNoTracking()
                    .SingleAsync(s => s.PlayerId == playerId, CancellationToken);

                Assert.Equal((int)EStatisticType.EnemiesKilled, stat.StatisticTypeId);
                Assert.Null(stat.EntityId);
                Assert.Equal(7m, stat.Value);
            }
        }

        [Fact]
        public async Task Save_WithoutPriorLoad_UpdatesExistingStatisticsWithoutDuplicating()
        {
            var playerId = await SeedPlayerAsync();
            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                await TestDataSeeder.AddPlayerStatisticAsync(context, playerId, EStatisticType.EnemiesKilled, 3m);
            }

            using (var scope = CreateScope())
            {
                var repo = scope.ServiceProvider.GetRequiredService<IPlayerProgressRepository>();
                var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                var progress = new PlayerProgress(
                    MakeDomainPlayer(playerId),
                    [new PlayerStatistic { Type = EStatisticType.EnemiesKilled, EntityId = null, Value = 10m }],
                    []);

                // No Load first: Save must fetch the existing (tracked) row on demand and update it.
                await repo.Save(progress);
                await unitOfWork.CommitAsync();
            }

            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                var stat = await context.PlayerStatistics.AsNoTracking()
                    .SingleAsync(s => s.PlayerId == playerId, CancellationToken);

                Assert.Equal(10m, stat.Value);
            }
        }

        [Fact]
        public async Task LoadThenSave_UpdatesExistingStatisticWithoutDuplicating()
        {
            var playerId = await SeedPlayerAsync();
            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                await TestDataSeeder.AddPlayerStatisticAsync(context, playerId, EStatisticType.EnemiesKilled, 3m);
            }

            using (var scope = CreateScope())
            {
                var repo = scope.ServiceProvider.GetRequiredService<IPlayerProgressRepository>();
                var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                var progress = await repo.Load(MakeDomainPlayer(playerId));
                progress.Statistics.Single().Value = 12m;

                // The common flow: the snapshot from Load is reused, so the existing row is updated in place.
                await repo.Save(progress);
                await unitOfWork.CommitAsync();
            }

            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                var stat = await context.PlayerStatistics.AsNoTracking()
                    .SingleAsync(s => s.PlayerId == playerId, CancellationToken);

                Assert.Equal(12m, stat.Value);
            }
        }

        [Fact]
        public async Task Save_WithoutPriorLoad_InsertsNewChallengeProgress()
        {
            var playerId = await SeedPlayerAsync();
            int challengeId;
            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                var challenge = await TestDataSeeder.CreateChallengeAsync(context);
                challengeId = challenge.Id;
            }

            var completedAt = DateTime.UtcNow;
            using (var scope = CreateScope())
            {
                var repo = scope.ServiceProvider.GetRequiredService<IPlayerProgressRepository>();
                var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                var playerChallenge = new PlayerChallenge(MakeDomainChallenge(challengeId), progress: 10m, completed: true, completedAt);
                var progress = new PlayerProgress(MakeDomainPlayer(playerId), [], [playerChallenge]);

                await repo.Save(progress);
                await unitOfWork.CommitAsync();
            }

            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                var entity = await context.PlayerChallenges.AsNoTracking()
                    .SingleAsync(c => c.PlayerId == playerId, CancellationToken);

                Assert.Equal(challengeId, entity.ChallengeId);
                Assert.Equal(10m, entity.Progress);
                Assert.True(entity.Completed);
                Assert.NotNull(entity.CompletedAt);
            }
        }

        [Fact]
        public async Task Load_ReturnsPersistedStatisticsAndChallengeProgress()
        {
            var playerId = await SeedPlayerAsync();
            int challengeId;
            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                await TestDataSeeder.AddPlayerStatisticAsync(context, playerId, EStatisticType.EnemiesKilled, 5m);
                var challenge = await TestDataSeeder.CreateChallengeAsync(context);
                challengeId = challenge.Id;
                await TestDataSeeder.AddPlayerChallengeAsync(context, playerId, challengeId, progress: 4m, completed: false);
            }

            // Reload the caches so the seeded challenge is resolvable by id (they no longer lazily refill).
            await ReloadReferenceCachesAsync();

            using (var scope = CreateScope())
            {
                var repo = scope.ServiceProvider.GetRequiredService<IPlayerProgressRepository>();

                var progress = await repo.Load(MakeDomainPlayer(playerId));

                var stat = progress.Statistics.Single();
                Assert.Equal(EStatisticType.EnemiesKilled, stat.Type);
                Assert.Equal(5m, stat.Value);

                var challengeProgress = progress.ChallengeProgress.Single();
                Assert.Equal(challengeId, challengeProgress.Challenge.Id);
                Assert.Equal(4m, challengeProgress.Progress);
                Assert.False(challengeProgress.Completed);
            }
        }

        [Fact]
        public async Task GetCompletedChallengeIds_ReturnsOnlyCompletedChallenges()
        {
            var playerId = await SeedPlayerAsync();
            int completedId;
            int incompleteId;
            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                var completed = await TestDataSeeder.CreateChallengeAsync(context, "Completed");
                var incomplete = await TestDataSeeder.CreateChallengeAsync(context, "Incomplete");
                completedId = completed.Id;
                incompleteId = incomplete.Id;
                await TestDataSeeder.AddPlayerChallengeAsync(context, playerId, completedId, progress: 10m, completed: true);
                await TestDataSeeder.AddPlayerChallengeAsync(context, playerId, incompleteId, progress: 4m, completed: false);
            }

            using (var scope = CreateScope())
            {
                var repo = scope.ServiceProvider.GetRequiredService<IPlayerProgressRepository>();

                var ids = await repo.GetCompletedChallengeIds(playerId);

                Assert.Equal(new HashSet<int> { completedId }, ids);
            }
        }

        [Fact]
        public async Task RecordBattleCompleted_PersistsDamageHealedStatistic()
        {
            // The DamageHealed statistic (fed by the DoT/HoT phase's PlayerDamageHealed) was defined but
            // unused before #334; this verifies it now flows through RecordBattleCompleted to a persisted row.
            var playerId = await SeedPlayerAsync();

            using (var scope = CreateScope())
            {
                var repo = scope.ServiceProvider.GetRequiredService<IPlayerProgressRepository>();
                var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                var progress = await repo.Load(MakeDomainPlayer(playerId));
                progress.RecordBattleCompleted(
                    MakeEnemy(), victory: true, playerDied: false, totalMs: 1000,
                    new BattleStats { PlayerDamageHealed = 12.5 }, isBossBattle: false, zoneId: 0);

                await repo.Save(progress);
                await unitOfWork.CommitAsync();
            }

            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                var stat = await context.PlayerStatistics.AsNoTracking()
                    .SingleAsync(
                        s => s.PlayerId == playerId && s.StatisticTypeId == (int)EStatisticType.DamageHealed,
                        CancellationToken);

                Assert.Equal(12.5m, stat.Value);
            }
        }

        private static Enemy MakeEnemy(int id = 1) => new()
        {
            Id = id,
            Name = "Test Enemy",
            Level = 1,
            IsBoss = false,
            AttributeDistributions = [],
            AvailableSkills = [],
        };

        private async Task<int> SeedPlayerAsync()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var user = await TestDataSeeder.CreateUserAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            return player.Id;
        }

        private static Player MakeDomainPlayer(int id) => new()
        {
            Id = id,
            Name = "Test",
            Level = 1,
            Exp = 0,
            CurrentZoneId = 0,
            StatPoints = new PlayerStatPoints([]) { StatPointsGained = 0, StatPointsUsed = 0 },
            Inventory = new Inventory(),
            SelectedSkills = [],
            Skills = [],
            LogPreferences = [],
        };

        private static Challenge MakeDomainChallenge(int id) => new()
        {
            Id = id,
            Name = "Test Challenge",
            Description = string.Empty,
            Type = new ChallengeType(EChallengeType.EnemiesKilled),
            TargetEntityId = null,
            ProgressGoal = 10m,
        };
    }
}
