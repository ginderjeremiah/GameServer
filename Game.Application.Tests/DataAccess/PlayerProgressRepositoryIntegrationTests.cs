using Game.Abstractions.DataAccess;
using Game.Abstractions.Infrastructure;
using Game.Core;
using Game.Core.Battle;
using Game.Core.Enemies;
using Game.Core.Events;
using Game.Core.Players;
using Game.Core.Players.Inventories;
using Game.Core.Progress;
using Game.DataAccess;
using Game.DataAccess.Repositories;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Base;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Xunit;

namespace Game.Application.Tests.DataAccess
{
    /// <summary>
    /// Verifies the write-behind <see cref="IPlayerProgressRepository"/> (#550): reads are served
    /// cache-first with a database miss-reload, and <see cref="IPlayerProgressRepository.Save"/> writes the
    /// cache (the source of truth) and enqueues a single batched, absolute-value <c>ProgressUpdated</c>
    /// event rather than writing the database synchronously. The database is converged off the response
    /// path by <see cref="DataProviderSynchronizer"/> (covered in its own tests). The integration harness
    /// disables that hosted service, so here a Save leaves the database untouched — which is exactly what
    /// lets these tests prove the reads come from the cache.
    /// </summary>
    [Collection("Integration")]
    public class PlayerProgressRepositoryIntegrationTests : ApplicationIntegrationTestBase
    {
        public PlayerProgressRepositoryIntegrationTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

        [Fact]
        public async Task Save_EnqueuesBatchedProgressUpdatedEvent_WithoutWritingTheDatabase()
        {
            var playerId = await SeedPlayerAsync();
            using var multiplexer = await ConnectRedisAsync();
            var redis = multiplexer.GetDatabase();
            Assert.Equal(0, await redis.ListLengthAsync(Constants.PUBSUB_PLAYER_QUEUE));

            using (var scope = CreateScope())
            {
                var repo = scope.ServiceProvider.GetRequiredService<IPlayerProgressRepository>();
                var progress = new PlayerProgress(MakeDomainPlayer(playerId), [], []);
                progress.RecordBattleCompleted(MakeEnemy(), victory: true, playerDied: false, totalMs: 1000,
                    new BattleStats { PlayerDamageDealt = 12.5 }, isBossBattle: false, zoneId: 0);

                await repo.Save(progress);
            }

            // Exactly one batched event was enqueued, carrying the changed stats as absolute values.
            Assert.Equal(1, await redis.ListLengthAsync(Constants.PUBSUB_PLAYER_QUEUE));
            var evt = await DequeueProgressEvent(redis);
            Assert.Equal(playerId, evt.PlayerId);
            Assert.Contains(evt.Statistics, s => s.StatisticTypeId == (int)EStatisticType.DamageDealt && s.Value == 12.5m);
            Assert.Contains(evt.Statistics, s => s.StatisticTypeId == (int)EStatisticType.EnemiesKilled && s.EntityId == null);

            // Nothing was written to the database synchronously (the synchronizer is disabled in tests).
            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                Assert.Empty(await context.PlayerStatistics.AsNoTracking()
                    .Where(s => s.PlayerId == playerId).ToListAsync(CancellationToken));
            }
        }

        [Fact]
        public async Task Save_NoMutationsSinceLoad_DoesNotEnqueue()
        {
            var playerId = await SeedPlayerAsync();
            using var multiplexer = await ConnectRedisAsync();
            var redis = multiplexer.GetDatabase();

            using var scope = CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IPlayerProgressRepository>();

            var progress = await repo.Load(MakeDomainPlayer(playerId));
            await repo.Save(progress); // nothing changed -> nothing to persist

            Assert.Equal(0, await redis.ListLengthAsync(Constants.PUBSUB_PLAYER_QUEUE));
        }

        [Fact]
        public async Task Save_WritesCache_SoASubsequentReadIsServedWithoutTheDatabase()
        {
            var playerId = await SeedPlayerAsync();

            using (var scope = CreateScope())
            {
                var repo = scope.ServiceProvider.GetRequiredService<IPlayerProgressRepository>();
                var progress = await repo.Load(MakeDomainPlayer(playerId)); // cache miss -> empty
                progress.RecordBattleCompleted(MakeEnemy(), victory: true, playerDied: false, totalMs: 1000,
                    new BattleStats(), isBossBattle: false, zoneId: 0);

                await repo.Save(progress); // writes the full snapshot to the cache (the source of truth)
            }

            // A fresh scope reads the value straight from the cache — the database was never written.
            using (var scope = CreateScope())
            {
                var repo = scope.ServiceProvider.GetRequiredService<IPlayerProgressRepository>();
                var stats = await repo.GetStatistics(playerId);
                Assert.Contains(stats, s => s.Type == EStatisticType.EnemiesKilled && s.EntityId == null && s.Value == 1m);
            }

            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                Assert.Empty(await context.PlayerStatistics.AsNoTracking()
                    .Where(s => s.PlayerId == playerId).ToListAsync(CancellationToken));
            }
        }

        [Fact]
        public async Task Load_CacheMiss_ReloadsStatisticsAndChallengeProgressFromDatabase()
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

                // The progress cache key is empty (Redis was flushed on setup), so Load reloads from the DB.
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
        public async Task Save_WhenSourceOfTruthCacheWriteFails_SurfacesTheFailure()
        {
            var playerId = await SeedPlayerAsync();

            using var scope = CreateScope();
            var sp = scope.ServiceProvider;

            // Wrap the real cache so the awaited source-of-truth write fails while reads/load still work. The
            // fix awaits that write, so a dropped write must propagate instead of being swallowed (#580).
            var throwingCache = new ThrowingOnSetCacheService(sp.GetRequiredService<ICacheService>());
            var repo = new PlayerProgressRepository(
                sp.GetRequiredService<GameContext>(),
                sp.GetRequiredService<IChallenges>(),
                throwingCache,
                sp.GetRequiredService<IPubSubService>());

            var progress = await repo.Load(MakeDomainPlayer(playerId));
            progress.RecordBattleCompleted(MakeEnemy(), victory: true, playerDied: false, totalMs: 1000,
                new BattleStats(), isBossBattle: false, zoneId: 0);

            await Assert.ThrowsAsync<CacheWriteFailedException>(() => repo.Save(progress));
        }

        private async Task<ConnectionMultiplexer> ConnectRedisAsync()
        {
            var options = ConfigurationOptions.Parse(Containers.PubSubConnectionString);
            return await ConnectionMultiplexer.ConnectAsync(options);
        }

        private static async Task<ProgressUpdatedEvent> DequeueProgressEvent(IDatabase redis)
        {
            var raw = await redis.ListLeftPopAsync(Constants.PUBSUB_PLAYER_QUEUE);
            Assert.False(raw.IsNull);

            var envelope = raw.ToString().Deserialize<DomainEventEnvelope>();
            Assert.NotNull(envelope);
            Assert.Equal(nameof(ProgressUpdatedEvent), envelope.Type);

            var evt = envelope.Payload.Deserialize<ProgressUpdatedEvent>();
            Assert.NotNull(evt);
            return evt;
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
    }
}
