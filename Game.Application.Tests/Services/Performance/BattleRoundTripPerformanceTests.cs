using System.Diagnostics;
using Game.Abstractions.DataAccess;
using Game.Abstractions.Infrastructure;
using Game.Application;
using Game.Application.Services;
using Game.Core.Players;
using Game.DataAccess;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Base;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Game.Application.Tests.Services.Performance
{
    /// <summary>
    /// Observability-first performance probes for the full battle-end <em>round trip</em> against real
    /// Postgres and Redis (spike #548). Unlike the CPU-only <c>BattlePerformanceTests</c> (which time the
    /// simulator in isolation with stubbed dependencies), these measure the synchronous I/O the response
    /// path actually pays per battle and log a per-phase breakdown so the write-behind spike has data:
    /// <list type="bullet">
    ///   <item><c>EndBattleVictory</c> — sim + progress <c>Load</c> (a cache hit since #556) + progress
    ///   <c>Save</c> (cache write + one queued event) + <c>SavePlayer</c> (Redis publish + cache write).</item>
    ///   <item><c>CommitAsync</c> — the per-command unit-of-work commit; a no-op for progress since the
    ///   write-behind moved the stat/challenge DB write off the response path (#556).</item>
    ///   <item>progress <c>Load</c> — measured both as a warm cache hit and a cold cache-miss DB reload.</item>
    ///   <item><c>SavePlayer</c> — the Redis publish + cache write, isolated.</item>
    /// </list>
    /// This test intentionally runs a short, lopsided victory whose simulation is sub-ms, so these figures
    /// isolate the (roughly constant) per-battle I/O. The simulation's full cost — which scales with battle
    /// length and loadout and <em>dominates</em> long battles — is characterized separately in
    /// <c>Game.Core.Tests</c>' <c>BattlePerformanceTests</c>; the response-path total is the sum of the two.
    /// These are deliberately <b>not</b> tight gates:
    /// real container latency is environment-dependent, so only a generous catastrophic ceiling is asserted
    /// on the full round trip and everything else is logged for tracking. Tagged <c>Performance</c> so the
    /// PR gate can exclude them in one line (<c>dotnet test -- --filter-not-trait "Category=Performance"</c>).
    /// </summary>
    [Trait("Category", "Performance")]
    [Collection("Integration")]
    public class BattleRoundTripPerformanceTests : ApplicationIntegrationTestBase
    {
        private const int WarmupIterations = 3;
        private const int SampleCount = 20;

        // Generous catastrophic-regression ceiling for one full battle-end round trip against local
        // containers (typically single-digit ms). Large enough never to flake on a slow runner while still
        // catching an order-of-magnitude blow-up (e.g. an accidental synchronous N+1 query on the path).
        private const double FullRoundTripCeilingMs = 1000.0;

        private readonly ITestOutputHelper _output;

        public BattleRoundTripPerformanceTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper)
        {
            _output = testOutputHelper;
        }

        [Fact]
        public async Task FullRoundTrip_EndBattleVictoryAndCommit_BreaksDownPhases()
        {
            var fixture = await SeedBattleFixtureAsync();

            var endStats = new List<double>(SampleCount);
            var commitStats = new List<double>(SampleCount);
            var totalStats = new List<double>(SampleCount);

            for (var i = 0; i < WarmupIterations; i++)
            {
                var warmup = await StartFreshBattleAsync(fixture);
                await warmup.BattleService.EndBattleVictory(warmup.Player, warmup.State, DateTime.UtcNow);
                await warmup.UnitOfWork.CommitAsync();
                warmup.Scope.Dispose();
            }

            for (var s = 0; s < SampleCount; s++)
            {
                var op = await StartFreshBattleAsync(fixture);
                Settle();

                var endMs = await TimeAsync(() => op.BattleService.EndBattleVictory(op.Player, op.State, DateTime.UtcNow));
                var commitMs = await TimeAsync(() => op.UnitOfWork.CommitAsync());

                endStats.Add(endMs);
                commitStats.Add(commitMs);
                totalStats.Add(endMs + commitMs);
                op.Scope.Dispose();
            }

            LogStats("EndBattleVictory (sim + progress read + SavePlayer/Redis)", endStats);
            LogStats("CommitAsync (Postgres write)", commitStats);
            LogStats("Full round trip", totalStats);

            var total = Summarize(totalStats);
            Assert.True(
                total.MinMs < FullRoundTripCeilingMs,
                $"Full battle-end round trip took {total.MinMs:F2} ms (min), exceeding the generous "
                + $"{FullRoundTripCeilingMs:F0} ms catastrophic-regression ceiling. The path should be far "
                + "faster than this; a breach points at a large I/O regression (e.g. an N+1 query).");
        }

        [Fact]
        public async Task ProgressLoad_CacheHit_ServedFromCache()
        {
            var fixture = await SeedBattleFixtureAsync();
            // The battles populate the progress cache via write-behind Save, so every Load below is a hit —
            // the common case since #556, and what makes the per-battle progress read effectively free.
            await RunBattlesAsync(fixture, count: 5);

            var stats = new List<double>(SampleCount);

            for (var i = 0; i < WarmupIterations + SampleCount; i++)
            {
                using var scope = CreateScope();
                var (_, player) = await LoadPlayerAsync(scope, fixture.PlayerId);
                var progressRepo = scope.ServiceProvider.GetRequiredService<IPlayerProgressRepository>();

                if (i < WarmupIterations)
                {
                    await progressRepo.Load(player);
                    continue;
                }

                Settle();
                stats.Add(await TimeAsync(() => progressRepo.Load(player)));
            }

            LogStats("Progress Load (cache hit)", stats);
            Assert.NotEmpty(stats);
        }

        [Fact]
        public async Task ProgressLoad_CacheMiss_ReloadsFromDatabase()
        {
            var fixture = await SeedBattleFixtureAsync();
            await RunBattlesAsync(fixture, count: 5);
            // The harness disables the synchronizer, so drain the write-behind queue into the DB by hand —
            // a cache miss must have real rows (a realistic battle-generated spread) to reload.
            await DrainPlayerUpdateQueueAsync();

            var key = $"{Constants.CACHE_PROGRESS_PREFIX}_{fixture.PlayerId}";
            var stats = new List<double>(SampleCount);

            for (var i = 0; i < WarmupIterations + SampleCount; i++)
            {
                using var scope = CreateScope();
                var (_, player) = await LoadPlayerAsync(scope, fixture.PlayerId);
                var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();
                var progressRepo = scope.ServiceProvider.GetRequiredService<IPlayerProgressRepository>();

                // Evict the progress key so this Load is a true cache miss — the two SELECT reloads + re-cache.
                await cache.Delete(key);

                if (i < WarmupIterations)
                {
                    await progressRepo.Load(player);
                    continue;
                }

                Settle();
                stats.Add(await TimeAsync(() => progressRepo.Load(player)));
            }

            LogStats("Progress Load (cache miss -> DB reload, 2 SELECTs)", stats);
            Assert.NotEmpty(stats);
        }

        // Drains the player update queue into the database via the synchronizer (the hosted service is
        // disabled in the integration harness), so cache-miss reloads have persisted rows to read.
        private async Task DrainPlayerUpdateQueueAsync()
        {
            using var scope = CreateScope();
            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            var synchronizer = new DataProviderSynchronizer(
                scope.ServiceProvider,
                pubsub,
                scope.ServiceProvider.GetRequiredService<ILogger<DataProviderSynchronizer>>(),
                new PlayerUpdateRetryPolicy(maxAttempts: 3, baseDelay: TimeSpan.Zero));

            await synchronizer.ProcessQueue(pubsub.GetQueue(Constants.PUBSUB_PLAYER_QUEUE));
        }

        [Fact]
        public async Task SavePlayer_RedisPublishAndCache()
        {
            var fixture = await SeedBattleFixtureAsync();

            var stats = new List<double>(SampleCount);

            for (var i = 0; i < WarmupIterations + SampleCount; i++)
            {
                using var scope = CreateScope();
                var (playerRepo, player) = await LoadPlayerAsync(scope, fixture.PlayerId);
                // Mutate the aggregate so a persistent PlayerCoreUpdatedEvent is queued on save.
                player.GrantExp(10);

                if (i < WarmupIterations)
                {
                    await playerRepo.SavePlayer(player);
                    continue;
                }

                Settle();
                stats.Add(await TimeAsync(() => playerRepo.SavePlayer(player)));
            }

            LogStats("SavePlayer (event dispatch + Redis publish + cache write)", stats);
            Assert.NotEmpty(stats);
        }

        // Seeds a minimal player-vs-enemy scenario in one zone and confirms (via a probe battle) that it
        // reliably resolves to a victory, so every measured op does the full pay-out work. The probe also
        // primes the player's statistic rows, so later Load measurements read real data.
        private async Task<BattleFixture> SeedBattleFixtureAsync()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var skill = await TestDataSeeder.CreateSkillAsync(context);
            var enemy = await TestDataSeeder.CreateEnemyAsync(context);
            await TestDataSeeder.LinkSkillToEnemyAsync(context, enemy.Id, skill.Id);
            var zone = await TestDataSeeder.CreateZoneAsync(context);
            await TestDataSeeder.LinkEnemyToZoneAsync(context, zone.Id, enemy.Id);

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id, zoneId: zone.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, playerEntity.Id, skill.Id);

            // Reference data was seeded directly; reload the caches so battle setup resolves it.
            await ReloadReferenceCachesAsync();

            var fixture = new BattleFixture(playerEntity.Id, zone.Id);

            var (_, player) = await LoadPlayerAsync(scope, fixture.PlayerId);
            var battleService = scope.ServiceProvider.GetRequiredService<BattleService>();
            var state = new PlayerState();
            await battleService.StartBattle(player, state, fixture.ZoneId);
            state.BattleStartTime = DateTime.UtcNow.AddMinutes(-10);

            var probe = await battleService.EndBattleVictory(player, state, DateTime.UtcNow);
            Assert.NotNull(probe); // the scenario must be a victory, or measured ops would do different work
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync();

            return fixture;
        }

        // Builds a fresh DI scope with an active backdated battle ready to be ended — the per-command unit
        // the real socket handler operates on (a new scope, a cache-loaded player, a started battle).
        private async Task<BattleOp> StartFreshBattleAsync(BattleFixture fixture)
        {
            var scope = CreateScope();
            var (_, player) = await LoadPlayerAsync(scope, fixture.PlayerId);
            var battleService = scope.ServiceProvider.GetRequiredService<BattleService>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var state = new PlayerState();
            await battleService.StartBattle(player, state, fixture.ZoneId);
            // Backdate so the claimed victory timestamp is valid (earliestDefeat well before now).
            state.BattleStartTime = DateTime.UtcNow.AddMinutes(-10);

            return new BattleOp(scope, battleService, unitOfWork, player, state);
        }

        private async Task RunBattlesAsync(BattleFixture fixture, int count)
        {
            for (var i = 0; i < count; i++)
            {
                var op = await StartFreshBattleAsync(fixture);
                await op.BattleService.EndBattleVictory(op.Player, op.State, DateTime.UtcNow);
                await op.UnitOfWork.CommitAsync();
                op.Scope.Dispose();
            }
        }

        private static async Task<(IPlayerRepository Repo, Player Player)> LoadPlayerAsync(IServiceScope scope, int playerId)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            var player = await repo.GetPlayer(playerId)
                ?? throw new InvalidOperationException($"Seeded player {playerId} was not found.");
            return (repo, player);
        }

        private static async Task<double> TimeAsync(Func<Task> op)
        {
            var stopwatch = Stopwatch.StartNew();
            await op();
            stopwatch.Stop();
            return stopwatch.Elapsed.TotalMilliseconds;
        }

        // The reported figure is the min across samples: timing noise (scheduling, GC, contention) can only
        // ever add time, so the fastest sample is the cleanest estimate. Median/mean are logged for context.
        private void LogStats(string label, IReadOnlyList<double> samplesMs)
        {
            var stats = Summarize(samplesMs);
            _output.WriteLine(
                $"{label}: min {stats.MinMs:F2} ms, median {stats.MedianMs:F2} ms, mean {stats.MeanMs:F2} ms "
                + $"(n={stats.Samples})");
        }

        private static Stats Summarize(IReadOnlyList<double> samplesMs)
        {
            var sorted = samplesMs.OrderBy(value => value).ToArray();
            var count = sorted.Length;
            var median = count % 2 == 1
                ? sorted[count / 2]
                : (sorted[(count / 2) - 1] + sorted[count / 2]) / 2.0;
            return new Stats(sorted[0], median, samplesMs.Average(), count);
        }

        private static void Settle()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        private sealed record BattleFixture(int PlayerId, int ZoneId);

        private sealed record BattleOp(
            IServiceScope Scope,
            BattleService BattleService,
            IUnitOfWork UnitOfWork,
            Player Player,
            PlayerState State);

        private sealed record Stats(double MinMs, double MedianMs, double MeanMs, int Samples);
    }
}
