using System.Globalization;
using Game.Application.Tests.DataAccess;
using Game.Core;
using Game.Core.Players.Events;
using Game.Core.TestInfrastructure.Performance;
using Game.DataAccess;
using Game.DataAccess.PlayerUpdates;
using Game.Infrastructure.Database;
using Game.Infrastructure.Entities;
using Game.TestInfrastructure.Base;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Game.Application.Tests.Services.Performance
{
    /// <summary>
    /// Measures what the <c>PlayerWriteWatermark</c> guard actually costs on the drain-side apply (#2497), so
    /// the round trips it adds to every guarded write are a recorded number in
    /// <c>docs/backend-persistence.md</c> rather than something discovered during an incident. The guard walks
    /// part of the one-round-trip-per-event ceiling back — the trade #1701's bounded-concurrency work was
    /// relieving — and that is worth knowing precisely even though the trade itself is right.
    /// <para>
    /// <b>The unguarded baseline is a real production path, not a test double.</b> An envelope carrying
    /// <see cref="DomainEventEnvelope.Unsequenced"/> bypasses the guard entirely and applies exactly as the
    /// pre-guard handler did (see <see cref="PlayerWriteWatermarkGuard"/>'s sentinel branch), so measuring the
    /// same handler at a real sequence and at the sentinel isolates the guard's transaction framing plus its
    /// conditional upsert with no second code path to keep honest.
    /// </para>
    /// Observability-first like <c>PlayerCachePersistencePerformanceTests</c> and
    /// <c>BattleRoundTripPerformanceTests</c>: only a generous catastrophic ceiling is asserted, everything
    /// else is logged for tracking. Deliberately no ratio budget on the guard's overhead — container latency
    /// dominates it, and pinning a timing ratio on shared CI hardware is the flake class #2452 is open about.
    /// <para>
    /// <b>The reported overhead is a conservative floor, not a midpoint.</b> Each variant warms up
    /// individually, but the dominant warming here is <em>process</em>-wide (the Npgsql connection pool,
    /// Postgres' page and plan caches) and the unguarded variant is always measured first, so whatever warming
    /// is left over accrues to the guarded run and makes it slightly cheaper than it would be in isolation.
    /// That biases toward the guard looking cheap — the direction of the conclusion these numbers support — so
    /// it is recorded rather than left for a reader to infer. It does not change the finding: the overhead is
    /// positive, consistent across runs, and far larger than plausible drift.
    /// </para>
    /// </summary>
    [Trait("Category", "Performance")]
    [Collection("Integration")]
    public class PlayerWriteWatermarkGuardPerformanceTests : ApplicationIntegrationTestBase
    {
        private const int WarmupIterations = 3;
        private const int SampleCount = 15;
        private const int OperationsPerSample = 1;

        // Every created input consumes one sequence, so this is what a guarded stream's watermark must hold
        // once a measurement finishes — the assertion that every measured apply really wrote.
        private const long AppliesPerMeasurement = WarmupIterations + (SampleCount * OperationsPerSample);

        // A battle-completion save's realistic dirty-row count (the issue's ~10-20), and a single-row event to
        // compare it against — the pair is what shows whether the guard's cost tracks the event's width or
        // multiplies by it.
        private const int WideDirtyRowCount = 20;
        private const int NarrowDirtyRowCount = 1;

        // Generous catastrophic-regression ceiling, mirroring the sibling persistence suite — real container
        // latency is environment-dependent, so this only catches an order-of-magnitude blow-up.
        private const double ApplyCeilingMs = 1000.0;

        private readonly ITestOutputHelper _output;

        public PlayerWriteWatermarkGuardPerformanceTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper)
        {
            _output = testOutputHelper;
        }

        [Fact]
        public async Task PlayerCoreUpdatedApply_GuardedComparedAgainstTheUnguardedPath()
        {
            // A player per variant, so each measurement starts from its own watermark row rather than one the
            // other variant already seeded.
            var guardedPlayerId = await SeedPlayerAsync();
            var unguardedPlayerId = await SeedPlayerAsync();

            var unguarded = await MeasureApplyAsync(iteration => CoreEvent(unguardedPlayerId, iteration), guarded: false);
            var guarded = await MeasureApplyAsync(iteration => CoreEvent(guardedPlayerId, iteration), guarded: true);

            Report("PlayerCoreUpdated apply", unguarded, guarded);

            await AssertBaselineWasUnguardedAsync(
                unguardedPlayerId, guardedPlayerId, PlayerWriteStream.PlayerCore, PlayerWriteWatermarkGuard.PlayerScopedTarget);

            // The one absolute write over the player row is a single ExecuteUpdateAsync, so this is the case
            // where the guard's framing is the largest share of the total — the widest ratio the guard shows.
            AssertUnderCeiling("PlayerCoreUpdated", guarded);
        }

        [Fact]
        public async Task ProgressUpdatedApply_GuardedComparedAgainstTheUnguardedPath_AtOneAndAtTwentyDirtyRows()
        {
            var narrowGuardedPlayerId = await SeedPlayerAsync();
            var narrowUnguardedPlayerId = await SeedPlayerAsync();
            var wideGuardedPlayerId = await SeedPlayerAsync();
            var wideUnguardedPlayerId = await SeedPlayerAsync();

            // Real enemy rows rather than arbitrary ints: PlayerStatistic.EntityId carries no FK today, but a
            // per-enemy statistic is what these rows actually are and the fixture shouldn't depend on that.
            var enemyIds = await SeedEnemiesAsync(WideDirtyRowCount);

            var narrowUnguarded = await MeasureApplyAsync(
                iteration => ProgressEvent(narrowUnguardedPlayerId, enemyIds, NarrowDirtyRowCount, iteration), guarded: false);
            var narrowGuarded = await MeasureApplyAsync(
                iteration => ProgressEvent(narrowGuardedPlayerId, enemyIds, NarrowDirtyRowCount, iteration), guarded: true);
            var wideUnguarded = await MeasureApplyAsync(
                iteration => ProgressEvent(wideUnguardedPlayerId, enemyIds, WideDirtyRowCount, iteration), guarded: false);
            var wideGuarded = await MeasureApplyAsync(
                iteration => ProgressEvent(wideGuardedPlayerId, enemyIds, WideDirtyRowCount, iteration), guarded: true);

            Report($"ProgressUpdated apply, {NarrowDirtyRowCount} dirty row", narrowUnguarded, narrowGuarded);
            Report($"ProgressUpdated apply, {WideDirtyRowCount} dirty rows", wideUnguarded, wideGuarded);

            // The claim this pair exists to check: the watermark advance is one statement over an unnest'd key
            // array regardless of how many targets it carries (PlayerWriteWatermarkGuard.AdvanceWatermarksAsync),
            // so widening the event should move the guard's overhead by roughly the extra rows the upsert
            // touches — not by a factor of twenty. Reported rather than asserted; the numbers, not a timing
            // ratio on a shared runner, are what the docs record.
            var narrowOverheadMs = (narrowGuarded.MinMicroseconds - narrowUnguarded.MinMicroseconds) / 1000.0;
            var wideOverheadMs = (wideGuarded.MinMicroseconds - wideUnguarded.MinMicroseconds) / 1000.0;
            _output.WriteLine(
                $"Guard overhead vs dirty-row count: {narrowOverheadMs:F3} ms at {NarrowDirtyRowCount} row -> "
                + $"{wideOverheadMs:F3} ms at {WideDirtyRowCount} rows. A per-target statement would show "
                + $"~{WideDirtyRowCount}x here; one upsert over the whole key set should not.");

            await AssertBaselineWasUnguardedAsync(
                narrowUnguardedPlayerId, narrowGuardedPlayerId, PlayerWriteStream.Progress, StatisticTarget(enemyIds[0]));
            await AssertBaselineWasUnguardedAsync(
                wideUnguardedPlayerId, wideGuardedPlayerId, PlayerWriteStream.Progress, StatisticTarget(enemyIds[WideDirtyRowCount - 1]));

            AssertUnderCeiling($"ProgressUpdated ({WideDirtyRowCount} dirty rows)", wideGuarded);
        }

        /// <summary>
        /// Pins the assumption the whole measurement rests on: that the unguarded variant genuinely took
        /// <c>PlayerWriteWatermarkGuard</c>'s unsequenced-sentinel branch, and that every guarded apply
        /// actually wrote rather than being rejected as stale.
        /// </summary>
        /// <remarks>
        /// Without this the suite has no way to fail if that branch changes shape — it would quietly report an
        /// overhead near zero and the docs would record a number that measures nothing. The sentinel's
        /// semantics are covered behaviourally in <c>PlayerWriteWatermarkIntegrationTests</c>; this checks the
        /// baseline at the point it is actually relied on.
        /// </remarks>
        private async Task AssertBaselineWasUnguardedAsync(
            int unguardedPlayerId,
            int guardedPlayerId,
            PlayerWriteStream stream,
            string targetKey)
        {
            using var scope = CreateScope();

            Assert.Null(await PlayerUpdateTestHelpers.ReadWatermarkAsync(
                scope, unguardedPlayerId, stream, targetKey, CancellationToken));

            // Every apply advanced it, so the last sequence stamped is also the count of applies made — a
            // lower value would mean some measured apply was rejected and skipped its data write.
            Assert.Equal(
                AppliesPerMeasurement,
                await PlayerUpdateTestHelpers.ReadWatermarkAsync(scope, guardedPlayerId, stream, targetKey, CancellationToken));
        }

        private void Report(string label, MeasurementResult unguarded, MeasurementResult guarded)
        {
            var unguardedMs = unguarded.MinMicroseconds / 1000.0;
            var guardedMs = guarded.MinMicroseconds / 1000.0;

            _output.WriteLine(
                $"{label}, unguarded (sequence 0, pre-guard path): {unguardedMs:F3} ms (min), "
                + $"{unguarded.MedianMicroseconds / 1000.0:F3} ms (median)");
            _output.WriteLine(
                $"{label}, guarded (BEGIN + conditional upsert + COMMIT): {guardedMs:F3} ms (min), "
                + $"{guarded.MedianMicroseconds / 1000.0:F3} ms (median)");
            _output.WriteLine(
                $"{label}, guard overhead: {guardedMs - unguardedMs:F3} ms (min), "
                + $"{guardedMs / unguardedMs:F2}x the unguarded apply.");
        }

        private static void AssertUnderCeiling(string label, MeasurementResult guarded)
        {
            var guardedMs = guarded.MinMicroseconds / 1000.0;
            Assert.True(
                guardedMs < ApplyCeilingMs,
                $"A guarded {label} apply took {guardedMs:F2} ms (min), exceeding the "
                + $"{ApplyCeilingMs:F0} ms catastrophic-regression ceiling.");
        }

        /// <summary>
        /// Measures one guarded (or unguarded) apply through its own DI scope, mirroring the synchronizer's
        /// per-event scope. Guarded applies stamp a strictly increasing sequence so every measured apply
        /// actually writes — a rejected apply skips the data write entirely and would measure the guard's
        /// cheapest path as if it were its cost.
        /// </summary>
        private async Task<MeasurementResult> MeasureApplyAsync<TEvent>(Func<int, TEvent> createEvent, bool guarded)
        {
            var scopesToDispose = new List<IServiceScope>();
            var iteration = 0;

            var result = await PerformanceMeasurement.MeasureAsync(
                createInput: () =>
                {
                    var scope = CreateScope();
                    scopesToDispose.Add(scope);
                    var current = ++iteration;
                    var sequence = guarded ? current : DomainEventEnvelope.Unsequenced;
                    PlayerUpdateTestHelpers.DescribeSequence(scope, sequence);
                    var handler = scope.ServiceProvider.GetRequiredService<IPlayerUpdateHandler<TEvent>>();
                    return Task.FromResult((Handler: handler, Event: createEvent(current)));
                },
                timedOperation: input => input.Handler.HandleAsync(input.Event),
                warmupIterations: WarmupIterations,
                sampleCount: SampleCount,
                operationsPerSample: OperationsPerSample);

            foreach (var scope in scopesToDispose)
            {
                scope.Dispose();
            }

            return result;
        }

        // Exp varies per iteration so no apply degenerates into writing back what the row already holds.
        private static PlayerCoreUpdatedEvent CoreEvent(int playerId, int iteration)
            => PlayerUpdateTestHelpers.CoreEvent(playerId, level: 5, exp: iteration);

        // The persisted target key, hand-spelled rather than taken from PlayerWriteWatermarkGuard.Target: a
        // test computing its expected key with the production formatter could not catch that formatter
        // changing (the same reasoning the guard's integration suite records for its own key helpers).
        private static string StatisticTarget(int enemyId)
            => $"stat:{((int)EStatisticType.EnemiesKilled).ToString(CultureInfo.InvariantCulture)}"
                + $":{enemyId.ToString(CultureInfo.InvariantCulture)}";

        private static ProgressUpdatedEvent ProgressEvent(int playerId, IReadOnlyList<int> enemyIds, int dirtyRowCount, int iteration)
            => new()
            {
                PlayerId = playerId,
                Statistics = [.. Enumerable.Range(0, dirtyRowCount).Select(i => new CachedPlayerStatistic
                {
                    StatisticTypeId = (int)EStatisticType.EnemiesKilled,
                    EntityId = enemyIds[i],
                    Value = iteration,
                })],
            };

        private async Task<int> SeedPlayerAsync()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            // Each test seeds several players, so the seeder's default username would collide on IX_Users_Username.
            var user = await TestDataSeeder.CreateUserAsync(context, username: TestDataSeeder.UniqueUsername("perf"));
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            return player.Id;
        }

        private async Task<IReadOnlyList<int>> SeedEnemiesAsync(int count)
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var enemyIds = new List<int>(count);
            for (var i = 0; i < count; i++)
            {
                enemyIds.Add((await TestDataSeeder.CreateEnemyAsync(context)).Id);
            }

            return enemyIds;
        }
    }
}
