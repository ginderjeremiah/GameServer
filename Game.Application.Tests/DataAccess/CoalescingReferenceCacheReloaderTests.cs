using Game.Abstractions.DataAccess;
using Game.DataAccess;
using Game.TestInfrastructure.Helpers;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Game.Application.Tests.DataAccess
{
    /// <summary>
    /// Pins the coalescing/retry behaviour of <see cref="CoalescingReferenceCacheReloader"/> in isolation:
    /// it has no out-of-process dependency (the Redis pub/sub glue around it is covered by
    /// <see cref="ReferenceCacheSynchronizerTests"/>), so its burst handling, failure handling, and
    /// lifecycle are classical unit tests over in-memory caches.
    /// </summary>
    public class CoalescingReferenceCacheReloaderTests
    {
        private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(10);

        [Fact]
        public async Task RunAsync_NotificationBurst_CoalescesIntoOneSweepOverEveryCache()
        {
            var first = new RecordingCache();
            var second = new RecordingCache();
            var policy = new ReferenceCacheReloadPolicy(TimeSpan.FromMilliseconds(250), maxAttempts: 1, baseDelay: TimeSpan.Zero);
            await using var harness = new Harness(policy, first, second);

            // The whole burst lands well inside the debounce window, so it must collapse into one sweep.
            for (var i = 0; i < 5; i++)
            {
                harness.Reloader.NotifyChanged();
            }

            await WaitUntilAsync(() => first.CompletedReloads == 1, "the first sweep to complete");

            // No further sweep may follow once the burst has been drained.
            await Task.Delay(policy.DebounceWindow * 3, TestContext.Current.CancellationToken);
            Assert.Equal(1, first.CompletedReloads);
            Assert.Equal(1, second.CompletedReloads);
        }

        [Fact]
        public async Task RunAsync_NotificationDuringSweep_RunsExactlyOneFollowUpSweep()
        {
            var cache = new BlockingCache();
            var policy = new ReferenceCacheReloadPolicy(TimeSpan.FromMilliseconds(10), maxAttempts: 1, baseDelay: TimeSpan.Zero);
            await using var harness = new Harness(policy, cache);

            harness.Reloader.NotifyChanged();
            await cache.WaitUntilEnteredAsync(WaitTimeout);

            // Several notifications land while the sweep is in flight; the drain happens before the reload,
            // so they must survive it — but collapsed into a single follow-up sweep.
            for (var i = 0; i < 3; i++)
            {
                harness.Reloader.NotifyChanged();
            }

            cache.ReleaseOne();
            await cache.WaitUntilEnteredAsync(WaitTimeout);
            cache.ReleaseOne();

            await WaitUntilAsync(() => cache.CompletedReloads == 2, "the follow-up sweep to complete");
            await Task.Delay(policy.DebounceWindow * 5, TestContext.Current.CancellationToken);
            Assert.Equal(2, cache.EnteredReloads);
        }

        [Fact]
        public async Task RunAsync_TransientFailure_RetriesAndCompletesTheSweep()
        {
            var cache = new RecordingCache { FailFirstAttempts = 1 };
            var policy = new ReferenceCacheReloadPolicy(TimeSpan.Zero, maxAttempts: 3, baseDelay: TimeSpan.Zero);
            await using var harness = new Harness(policy, cache);

            harness.Reloader.NotifyChanged();

            await WaitUntilAsync(() => cache.CompletedReloads == 1, "the sweep to complete after a retry");
            Assert.Equal(2, cache.Attempts);
            Assert.Single(harness.Logs.Entries, e => e.Level == LogLevel.Warning);
            Assert.DoesNotContain(harness.Logs.Entries, e => e.Level == LogLevel.Error);
        }

        [Fact]
        public async Task RunAsync_RetriesExhausted_LogsErrorAndStillServesLaterNotifications()
        {
            // Both attempts of the first sweep fail; the holders' previous snapshots keep serving (pinned by
            // ReferenceCacheHolderTests), so the reloader's job is to log and stay alive for the next signal.
            var cache = new RecordingCache { FailFirstAttempts = 2 };
            var policy = new ReferenceCacheReloadPolicy(TimeSpan.Zero, maxAttempts: 2, baseDelay: TimeSpan.Zero);
            await using var harness = new Harness(policy, cache);

            harness.Reloader.NotifyChanged();

            await WaitUntilAsync(
                () => harness.Logs.Entries.Any(e => e.Level == LogLevel.Error),
                "the exhausted retries to log an error");
            Assert.Equal(2, cache.Attempts);
            Assert.Single(harness.Logs.Entries, e => e.Level == LogLevel.Warning);
            Assert.Equal(0, cache.CompletedReloads);

            // The loop survived the failed sweep: a later notification reloads successfully.
            harness.Reloader.NotifyChanged();
            await WaitUntilAsync(() => cache.CompletedReloads == 1, "a later sweep to complete");
        }

        [Fact]
        public async Task NotifyChanged_BeforeRunAsync_IsNotLost()
        {
            var cache = new RecordingCache();
            var policy = new ReferenceCacheReloadPolicy(TimeSpan.Zero, maxAttempts: 1, baseDelay: TimeSpan.Zero);

            // The signal is buffered, so a notification raised before the loop starts still triggers a sweep.
            var reloader = CreateReloader(policy, new CapturingLoggerProvider(), cache);
            reloader.NotifyChanged();

            using var cts = new CancellationTokenSource();
            var runTask = reloader.RunAsync(cts.Token);

            await WaitUntilAsync(() => cache.CompletedReloads == 1, "the buffered signal to trigger a sweep");

            cts.Cancel();
            await runTask.WaitAsync(WaitTimeout, TestContext.Current.CancellationToken);
        }

        [Fact]
        public async Task RunAsync_CancelledMidSweep_StopsWithoutCompletingTheSweep()
        {
            var cache = new BlockingCache();
            var policy = new ReferenceCacheReloadPolicy(TimeSpan.Zero, maxAttempts: 1, baseDelay: TimeSpan.Zero);
            var harness = new Harness(policy, cache);

            harness.Reloader.NotifyChanged();
            await cache.WaitUntilEnteredAsync(WaitTimeout);

            // Disposal cancels the loop while the sweep is blocked inside a cache reload; the loop must
            // unwind promptly instead of hanging on the in-flight reload.
            await harness.DisposeAsync();
            Assert.Equal(0, cache.CompletedReloads);
        }

        private static CoalescingReferenceCacheReloader CreateReloader(
            ReferenceCacheReloadPolicy policy,
            CapturingLoggerProvider logs,
            params IReloadableReferenceCache[] caches)
        {
            var loggerFactory = new LoggerFactory([logs]);
            return new CoalescingReferenceCacheReloader(caches, policy, loggerFactory.CreateLogger<CoalescingReferenceCacheReloader>());
        }

        private static async Task WaitUntilAsync(Func<bool> condition, string description)
        {
            var deadline = DateTime.UtcNow + WaitTimeout;
            while (!condition())
            {
                if (DateTime.UtcNow > deadline)
                {
                    Assert.Fail($"Timed out waiting for {description}.");
                }

                await Task.Delay(10, TestContext.Current.CancellationToken);
            }
        }

        /// <summary>Runs a reloader loop for the duration of a test and tears it down deterministically.</summary>
        private sealed class Harness : IAsyncDisposable
        {
            private readonly CancellationTokenSource _cts = new();
            private readonly Task _runTask;

            public CoalescingReferenceCacheReloader Reloader { get; }
            public CapturingLoggerProvider Logs { get; } = new();

            public Harness(ReferenceCacheReloadPolicy policy, params IReloadableReferenceCache[] caches)
            {
                Reloader = CreateReloader(policy, Logs, caches);
                _runTask = Reloader.RunAsync(_cts.Token);
            }

            public async ValueTask DisposeAsync()
            {
                _cts.Cancel();
                await _runTask.WaitAsync(WaitTimeout);
                _cts.Dispose();
            }
        }

        /// <summary>Counts reload attempts/completions and optionally fails the first N attempts.</summary>
        private sealed class RecordingCache : IReloadableReferenceCache
        {
            private int _attempts;
            private int _completedReloads;

            public int FailFirstAttempts { get; init; }
            public int Attempts => Volatile.Read(ref _attempts);
            public int CompletedReloads => Volatile.Read(ref _completedReloads);

            public Task ReloadAsync(CancellationToken cancellationToken = default)
            {
                var attempt = Interlocked.Increment(ref _attempts);
                if (attempt <= FailFirstAttempts)
                {
                    throw new InvalidOperationException($"Reload attempt {attempt} failed.");
                }

                Interlocked.Increment(ref _completedReloads);
                return Task.CompletedTask;
            }
        }

        /// <summary>Blocks each reload until released, so a test can act while a sweep is in flight.</summary>
        private sealed class BlockingCache : IReloadableReferenceCache
        {
            private readonly SemaphoreSlim _entered = new(0);
            private readonly SemaphoreSlim _release = new(0);
            private int _enteredReloads;
            private int _completedReloads;

            public int EnteredReloads => Volatile.Read(ref _enteredReloads);
            public int CompletedReloads => Volatile.Read(ref _completedReloads);

            public Task WaitUntilEnteredAsync(TimeSpan timeout) => _entered.WaitAsync(TestContext.Current.CancellationToken).WaitAsync(timeout);

            public void ReleaseOne() => _release.Release();

            public async Task ReloadAsync(CancellationToken cancellationToken = default)
            {
                Interlocked.Increment(ref _enteredReloads);
                _entered.Release();
                await _release.WaitAsync(cancellationToken);
                Interlocked.Increment(ref _completedReloads);
            }
        }
    }
}
