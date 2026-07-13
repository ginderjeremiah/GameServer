using Game.Abstractions.DataAccess;
using Game.Abstractions.Infrastructure;
using Game.Core;
using Game.Core.Battle;
using Game.Core.Enemies;
using Game.Core.Events;
using Game.Core.Players;
using Game.Core.Progress;
using Game.Core.TestInfrastructure.Builders;
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
                var progress = new PlayerProgress(MakeDomainPlayer(playerId), [], [], []);
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
        public async Task Save_SavingTheSameAggregateAgainWithoutFurtherMutation_EnqueuesNothing()
        {
            var playerId = await SeedPlayerAsync();
            using var multiplexer = await ConnectRedisAsync();
            var redis = multiplexer.GetDatabase();

            using var scope = CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IPlayerProgressRepository>();
            var progress = new PlayerProgress(MakeDomainPlayer(playerId), [], [], []);
            progress.RecordBattleCompleted(MakeEnemy(), victory: true, playerDied: false, totalMs: 1000,
                new BattleStats(), isBossBattle: false, zoneId: 0);

            await repo.Save(progress);
            Assert.Equal(1, await redis.ListLengthAsync(Constants.PUBSUB_PLAYER_QUEUE));

            // Saving the exact same aggregate a second time, with no new mutation, must not re-enqueue the
            // rows the first save already persisted — Save clears the dirty tracking once its own envelope
            // is buffered (the double-publish sharp edge a future multi-step caller could otherwise hit).
            await repo.Save(progress);
            Assert.Equal(1, await redis.ListLengthAsync(Constants.PUBSUB_PLAYER_QUEUE));
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
        public async Task Save_PublishFails_LeavesCacheUnadvanced()
        {
            var playerId = await SeedPlayerAsync();

            // A first successful save establishes a known snapshot in the cache (one kill).
            using (var scope = CreateScope())
            {
                var repo = scope.ServiceProvider.GetRequiredService<IPlayerProgressRepository>();
                var progress = await repo.Load(MakeDomainPlayer(playerId));
                progress.RecordBattleCompleted(MakeEnemy(), victory: true, playerDied: false, totalMs: 1000,
                    new BattleStats(), isBossBattle: false, zoneId: 0);
                await repo.Save(progress);
            }

            // A second save whose publish fails (a pre-cancelled budget makes the enqueue throw) must leave the
            // cache holding the pre-failure snapshot — publishing happens before the cache is advanced, so the
            // un-enqueued second kill never reaches the cache.
            using (var scope = CreateScope())
            {
                var repo = scope.ServiceProvider.GetRequiredService<IPlayerProgressRepository>();
                var progress = await repo.Load(MakeDomainPlayer(playerId)); // cache hit -> one kill
                progress.RecordBattleCompleted(MakeEnemy(), victory: true, playerDied: false, totalMs: 1000,
                    new BattleStats(), isBossBattle: false, zoneId: 0); // would advance to two

                using var cts = new CancellationTokenSource();
                await cts.CancelAsync();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repo.Save(progress, cts.Token));
            }

            // The cache still serves the snapshot from before the failed save (one kill, not two).
            using (var scope = CreateScope())
            {
                var repo = scope.ServiceProvider.GetRequiredService<IPlayerProgressRepository>();
                var stats = await repo.GetStatistics(playerId);
                var kills = Assert.Single(stats, s => s.Type == EStatisticType.EnemiesKilled && s.EntityId == null);
                Assert.Equal(1m, kills.Value);
            }
        }

        [Fact]
        public async Task Save_StandalonePublishFailsForANonCancellationReason_ThrowsPlayerPersistenceFlushFailedException()
        {
            var playerId = await SeedPlayerAsync();

            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();
            var challenges = scope.ServiceProvider.GetRequiredService<IChallenges>();
            var batch = scope.ServiceProvider.GetRequiredService<PlayerUpdateBatch>();

            // A repository built with a pubsub that throws for a reason other than cancellation stands in for a
            // transient Redis blip on a *standalone* progress save's flush (no player-save batch scope open) —
            // Save must wrap it the same way SavePlayer does, so the socket layer can force the connection's
            // in-memory Player to reload afterward rather than letting the caller's already-applied mutations
            // ride along un-persisted (#1819).
            var throwingRepo = new PlayerProgressRepository(context, challenges, cache, new ThrowingPubSubService(), batch);

            var progress = await throwingRepo.Load(MakeDomainPlayer(playerId));
            progress.RecordBattleCompleted(MakeEnemy(), victory: true, playerDied: false, totalMs: 1000,
                new BattleStats(), isBossBattle: false, zoneId: 0);

            var ex = await Assert.ThrowsAsync<PlayerPersistenceFlushFailedException>(() => throwingRepo.Save(progress));
            Assert.IsType<InvalidOperationException>(ex.InnerException);

            // The cache must not have advanced past the failed flush (publish-before-cache), so a fresh read via
            // a healthy repository still sees no statistics rather than the un-enqueued kill.
            using var readScope = CreateScope();
            var readRepo = readScope.ServiceProvider.GetRequiredService<IPlayerProgressRepository>();
            var stats = await readRepo.GetStatistics(playerId);
            Assert.DoesNotContain(stats, s => s.Type == EStatisticType.EnemiesKilled && s.EntityId == null);
        }

        [Fact]
        public async Task Save_DuringAPlayerSave_DefersToTheSharedBatchFlush_InsteadOfItsOwnRoundTrip()
        {
            var playerId = await SeedPlayerAsync();
            using var multiplexer = await ConnectRedisAsync();
            var redis = multiplexer.GetDatabase();
            Assert.Equal(0, await redis.ListLengthAsync(Constants.PUBSUB_PLAYER_QUEUE));

            using var scope = CreateScope();
            // The repo and the batch are resolved from the same scope, so they share the one scoped instance —
            // exactly as PlayerRepository and PlayerProgressRepository do within a socket command.
            var repo = scope.ServiceProvider.GetRequiredService<IPlayerProgressRepository>();
            var batch = scope.ServiceProvider.GetRequiredService<PlayerUpdateBatch>();
            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();

            // Load first, mirroring the real battle-completion path (BattleStatisticsEventHandler always loads
            // before saving) — it's also what establishes the cache key a fresh player's first save can then
            // advance (#1761), rather than constructing the aggregate directly and skipping that step.
            var progress = await repo.Load(MakeDomainPlayer(playerId));
            progress.RecordBattleCompleted(MakeEnemy(), victory: true, playerDied: false, totalMs: 1000,
                new BattleStats(), isBossBattle: false, zoneId: 0);

            // A progress save raised within a player save (the live battle-completion path) must NOT publish on
            // its own — it buffers into the shared batch so the player save's single flush carries it (#1237).
            using (batch.BeginPlayerSave())
            {
                await repo.Save(progress);
            }
            Assert.Equal(0, await redis.ListLengthAsync(Constants.PUBSUB_PLAYER_QUEUE));

            // The player save then flushes the shared batch once (mirroring PlayerRepository.SavePlayer) and
            // runs the deferred cache advance — the event reaches the queue (collapsed onto that single flush)
            // and the cache serves the snapshot afterwards.
            await batch.FlushAsync(pubsub);

            Assert.Equal(1, await redis.ListLengthAsync(Constants.PUBSUB_PLAYER_QUEUE));
            var evt = await DequeueProgressEvent(redis);
            Assert.Equal(playerId, evt.PlayerId);
            Assert.Contains(evt.Statistics, s => s.StatisticTypeId == (int)EStatisticType.EnemiesKilled && s.EntityId == null);

            using var readScope = CreateScope();
            var readRepo = readScope.ServiceProvider.GetRequiredService<IPlayerProgressRepository>();
            var stats = await readRepo.GetStatistics(playerId);
            Assert.Contains(stats, s => s.Type == EStatisticType.EnemiesKilled && s.EntityId == null && s.Value == 1m);
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
        public async Task Save_EnqueuesProficiencyProgress_AndServesItFromCacheWithoutTheDatabase()
        {
            var playerId = await SeedPlayerAsync();
            int proficiencyId;
            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                proficiencyId = (await TestDataSeeder.CreateProficiencyAsync(context)).Id;
            }

            using var multiplexer = await ConnectRedisAsync();
            var redis = multiplexer.GetDatabase();

            using (var scope = CreateScope())
            {
                var repo = scope.ServiceProvider.GetRequiredService<IPlayerProgressRepository>();
                var progress = await repo.Load(MakeDomainPlayer(playerId)); // cache miss -> empty
                progress.SetProficiencyProgress(proficiencyId, level: 2, xp: 130m);

                await repo.Save(progress); // enqueues the changed proficiency and advances the cache snapshot
            }

            // The batched event carries the proficiency as absolute level/XP.
            var evt = await DequeueProgressEvent(redis);
            Assert.Equal(playerId, evt.PlayerId);
            var enqueued = Assert.Single(evt.Proficiencies);
            Assert.Equal(proficiencyId, enqueued.ProficiencyId);
            Assert.Equal(2, enqueued.Level);
            Assert.Equal(130m, enqueued.Xp);

            // A fresh scope reads the proficiency straight from the cache — the database was never written
            // (the synchronizer is disabled in tests).
            using (var scope = CreateScope())
            {
                var repo = scope.ServiceProvider.GetRequiredService<IPlayerProgressRepository>();
                var proficiencies = await repo.GetProficiencies(playerId);
                var proficiency = Assert.Single(proficiencies);
                Assert.Equal(proficiencyId, proficiency.ProficiencyId);
                Assert.Equal(2, proficiency.Level);
                Assert.Equal(130m, proficiency.Xp);
            }

            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                Assert.Empty(await context.PlayerProficiencies.AsNoTracking()
                    .Where(pp => pp.PlayerId == playerId).ToListAsync(CancellationToken));
            }
        }

        [Fact]
        public async Task Load_CacheMiss_ReloadsProficiencyProgressFromDatabase()
        {
            var playerId = await SeedPlayerAsync();
            int proficiencyId;
            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                proficiencyId = (await TestDataSeeder.CreateProficiencyAsync(context)).Id;
                await TestDataSeeder.AddPlayerProficiencyAsync(context, playerId, proficiencyId, level: 3, xp: 275m);
            }

            using var scope2 = CreateScope();
            var repo = scope2.ServiceProvider.GetRequiredService<IPlayerProgressRepository>();

            // The progress cache key is empty (Redis was flushed on setup), so Load reloads from the DB.
            var progress = await repo.Load(MakeDomainPlayer(playerId));

            var proficiency = Assert.Single(progress.Proficiencies);
            Assert.Equal(proficiencyId, proficiency.ProficiencyId);
            Assert.Equal(3, proficiency.Level);
            Assert.Equal(275m, proficiency.Xp);
        }

        [Fact]
        public async Task Save_OnlyWritesTheRowsThatChanged_LeavingEarlierCachedRowsInTheHashUntouched()
        {
            var playerId = await SeedPlayerAsync();
            int proficiencyId;
            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                proficiencyId = (await TestDataSeeder.CreateProficiencyAsync(context)).Id;
            }

            using var multiplexer = await ConnectRedisAsync();
            var redis = multiplexer.GetDatabase();
            var hashKey = $"Progress_{playerId}";

            using (var scope = CreateScope())
            {
                var repo = scope.ServiceProvider.GetRequiredService<IPlayerProgressRepository>();
                var progress = await repo.Load(MakeDomainPlayer(playerId)); // cache miss -> empty
                progress.RecordBattleCompleted(MakeEnemy(), victory: true, playerDied: false, totalMs: 1000,
                    new BattleStats(), isBossBattle: false, zoneId: 0);
                await repo.Save(progress); // first save: creates the hash with several statistic fields
            }

            // The cache advance is a fire-and-forget HSET (Save returns once the write is dispatched, not once
            // it lands), so poll rather than reading immediately — otherwise this races the write and sees a
            // partial (or empty) hash (#1718).
            var fieldsAfterFirstSave = await WaitForHashFieldCountAsync(redis, hashKey, minCount: 2);
            Assert.True(fieldsAfterFirstSave.Length > 1, "Expected the battle-completion save to touch more than one statistic row.");

            using (var scope = CreateScope())
            {
                var repo = scope.ServiceProvider.GetRequiredService<IPlayerProgressRepository>();
                var progress = await repo.Load(MakeDomainPlayer(playerId)); // cache hit
                progress.SetProficiencyProgress(proficiencyId, level: 1, xp: 10m); // the only row dirtied this save
                await repo.Save(progress);
            }

            // The second save's HSET only wrote the one new proficiency field — every field the first save
            // wrote is still present, proving a save's cache write is scoped to its own dirty rows rather than
            // re-serializing (and re-sending) the whole cached snapshot (#1635).
            var fieldsAfterSecondSave = await WaitForHashFieldCountAsync(redis, hashKey, minCount: fieldsAfterFirstSave.Length + 1);
            Assert.Equal(fieldsAfterFirstSave.Length + 1, fieldsAfterSecondSave.Length);
            foreach (var field in fieldsAfterFirstSave)
            {
                Assert.Contains(fieldsAfterSecondSave, f => f.Name == field.Name && f.Value == field.Value);
            }
        }

        [Fact]
        public async Task Save_CacheKeyVanishesBetweenLoadAndSave_DoesNotResurrectAPartialHash()
        {
            var playerId = await SeedPlayerAsync();
            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                await TestDataSeeder.AddPlayerStatisticAsync(context, playerId, EStatisticType.EnemiesKilled, 5m);
            }

            using var multiplexer = await ConnectRedisAsync();
            var redis = multiplexer.GetDatabase();
            var hashKey = $"Progress_{playerId}";

            using (var scope = CreateScope())
            {
                var repo = scope.ServiceProvider.GetRequiredService<IPlayerProgressRepository>();

                // Cache miss -> DB reload establishes the full snapshot (the pre-existing 5 kills) in the cache.
                var progress = await repo.Load(MakeDomainPlayer(playerId));
                await WaitForHashFieldCountAsync(redis, hashKey, minCount: 1);

                // Simulate the key vanishing (Redis eviction under memory pressure, or an operator delete)
                // between this load and the save below — the exact race #1761 describes.
                await redis.KeyDeleteAsync(hashKey);

                progress.RecordBattleCompleted(MakeEnemy(), victory: true, playerDied: false, totalMs: 1000,
                    new BattleStats(), isBossBattle: false, zoneId: 0);
                await repo.Save(progress);
            }

            // The advance must not resurrect the key from this save's partial (dirty-only) view — it holds
            // only the incremented kill count, not the pre-existing 5. Give the fire-and-forget advance a
            // moment to (not) land, then assert the key is still gone.
            await Task.Delay(200);
            Assert.False(await redis.KeyExistsAsync(hashKey), "The cache advance must not recreate a key that vanished mid-flight from a partial dirty-row view.");

            // A subsequent read falls through to the DB (the write-behind synchronizer is disabled in this
            // harness, so the just-enqueued event never applied) and correctly serves the original 5 kills —
            // not a shadowed/regressed value from a resurrected partial hash.
            using (var scope = CreateScope())
            {
                var repo = scope.ServiceProvider.GetRequiredService<IPlayerProgressRepository>();
                var stats = await repo.GetStatistics(playerId);
                var kills = Assert.Single(stats, s => s.Type == EStatisticType.EnemiesKilled && s.EntityId == null);
                Assert.Equal(5m, kills.Value);
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

        private async Task<ConnectionMultiplexer> ConnectRedisAsync()
        {
            var options = ConfigurationOptions.Parse(Containers.PubSubConnectionString);
            return await ConnectionMultiplexer.ConnectAsync(options);
        }

        // HashSetAndForget dispatches its HSET with CommandFlags.FireAndForget (RedisService.cs), so it can
        // return before the write lands on the server — a read on a separate connection right after a Save can
        // race it. Poll until the expected field count shows up rather than asserting on a single read (#1718).
        private static Task<HashEntry[]> WaitForHashFieldCountAsync(IDatabase redis, string hashKey, int minCount, int timeoutMs = 5000)
        {
            return PollingHelper.PollUntilAsync(() => redis.HashGetAllAsync(hashKey), fields => fields.Length >= minCount, timeoutMs);
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

        private static Player MakeDomainPlayer(int id) => new PlayerBuilder().WithId(id).Build();
    }
}
