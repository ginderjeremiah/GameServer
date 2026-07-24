using Game.Infrastructure.Cache;
using Game.Infrastructure.PubSub;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;

namespace Game.Infrastructure.Redis
{
    internal static class RedisMultiplexerFactory
    {
        // Multiplexers are keyed by the *original* connection string. Keying by the raw string (rather than
        // comparing against ConnectionMultiplexer.Configuration, which StackExchange.Redis normalizes/rewrites)
        // makes reuse reliable: the cache and pub/sub services share one multiplexer whenever their connection
        // strings match.
        private static readonly Dictionary<string, IConnectionMultiplexer> _multiplexers = new();

        // A connect attempt that failed recently, keyed the same way. Checked before paying for another ~5s
        // Connect() so an outage fails callers fast instead of serializing them one behind another (#2371).
        private static readonly Dictionary<string, ConnectFailure> _recentFailures = new();
        private static readonly object _lock = new();

        // How long a failed connect is remembered before the next resolver is allowed to retry. Bounds the
        // fail-fast window to roughly one connect attempt's worth of time so the app notices Redis recovering
        // promptly, while still collapsing a burst of concurrent callers during an outage onto one attempt.
        private static readonly TimeSpan FailureBackoff = TimeSpan.FromSeconds(5);

        // Minimum size to grow the thread pool to before connecting (see ConnectMultiplexer).
        private const int MinThreadPoolThreads = 32;

        public static IConnectionMultiplexer GetMultiplexer(ICacheOptions config, ILogger logger)
        {
            if (config.CacheConnectionString is null)
            {
                throw new InvalidOperationException($"{nameof(config.CacheConnectionString)} cannot be null.");
            }

            return GetOrConnect(config.CacheConnectionString, logger);
        }

        public static IConnectionMultiplexer GetMultiplexer(IPubSubOptions config, ILogger logger)
        {
            if (config.PubSubConnectionString is null)
            {
                throw new InvalidOperationException($"{nameof(config.PubSubConnectionString)} cannot be null.");
            }

            return GetOrConnect(config.PubSubConnectionString, logger);
        }

        private static IConnectionMultiplexer GetOrConnect(string connectionString, ILogger logger)
        {
            return GetOrConnect(_multiplexers, _recentFailures, _lock, connectionString, ConnectMultiplexer, m => DiscardLoser(m, logger), logger, () => DateTime.UtcNow);
        }

        /// <summary>
        /// Closes and disposes every cached multiplexer and clears any remembered connect failures, so a later
        /// request reconnects fresh. Called from the host's graceful-shutdown hook (<see cref="RedisConnectionLifetime"/>)
        /// so the process-lifetime connections are torn down cleanly on stop rather than left to be force-killed
        /// (#954). Each connection is closed independently so one faulting close still lets the rest tear down.
        /// </summary>
        public static async Task DisposeAllAsync(ILogger logger)
        {
            await DisposeAllAsync(_multiplexers, _lock, logger);

            lock (_lock)
            {
                _recentFailures.Clear();
            }
        }

        /// <summary>
        /// Locked drain-and-dispose of a multiplexer cache: snapshots the values under <paramref name="syncRoot"/>,
        /// clears the cache, then disposes each entry. Generic and seam-extracted so the dispose/clear semantics
        /// are unit-testable with a fake disposable, mirroring how <see cref="GetOrConnect{T}"/> is tested without a
        /// live connection (#954). Each entry's dispose is wrapped independently so a faulting close is logged and
        /// skipped rather than aborting the rest of the drain.
        /// </summary>
        internal static async Task DisposeAllAsync<T>(Dictionary<string, T> cache, object syncRoot, ILogger logger)
            where T : IAsyncDisposable
        {
            List<T> toDispose;
            lock (syncRoot)
            {
                toDispose = [.. cache.Values];
                cache.Clear();
            }

            foreach (var disposable in toDispose)
            {
                try
                {
                    await disposable.DisposeAsync();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to close a cached Redis multiplexer during shutdown; continuing to close the rest.");
                }
            }
        }

        /// <summary>
        /// Locked get-or-connect with a fail-fast negative cache and a connect step that runs *outside* the lock
        /// (#2371). The lock only ever guards short dictionary reads/writes: whether <paramref name="key"/> is
        /// already connected, whether a recent failure is still within <see cref="FailureBackoff"/> (throw the
        /// remembered exception immediately rather than retry), and publishing the result of a connect attempt.
        /// The potentially-slow <paramref name="factory"/> call itself is never made under the lock, so a Redis
        /// outage no longer serializes every resolver behind one blocking ~5s connect apiece.
        /// Two callers can race a first-time connect for the same key since neither holds the lock while
        /// connecting; whichever publishes first wins and the loser's already-open connection is discarded via
        /// <paramref name="discardLoser"/> — every future caller for that key still shares the one winning
        /// instance (#696), even though the connect attempt itself wasn't serialized.
        /// </summary>
        internal static T GetOrConnect<T>(
            Dictionary<string, T> cache,
            Dictionary<string, ConnectFailure> recentFailures,
            object syncRoot,
            string key,
            Func<string, T> factory,
            Action<T> discardLoser,
            ILogger logger,
            Func<DateTime> utcNow)
        {
            lock (syncRoot)
            {
                if (cache.TryGetValue(key, out var existing))
                {
                    return existing;
                }

                if (recentFailures.TryGetValue(key, out var failure) && utcNow() - failure.OccurredAtUtc < FailureBackoff)
                {
                    // Fail fast: rethrow the remembered failure rather than pay for another attempt this soon.
                    throw failure.Error;
                }
            }

            T created;
            try
            {
                created = factory(key);
            }
            catch (Exception ex)
            {
                lock (syncRoot)
                {
                    recentFailures[key] = new ConnectFailure(utcNow(), ex);
                }

                logger.LogWarning(ex, "Redis connect failed; remembering the failure for {Backoff} so concurrent/subsequent resolvers fail fast instead of each retrying immediately.", FailureBackoff);
                throw;
            }

            lock (syncRoot)
            {
                if (cache.TryGetValue(key, out var winner))
                {
                    discardLoser(created);
                    return winner;
                }

                cache[key] = created;
                recentFailures.Remove(key);
                return created;
            }
        }

        // Delegates to the production drain-clear-dispose so the test reset can't drift from it (it previously
        // duplicated the logic with synchronous Dispose() where the production seam uses DisposeAsync()).
        internal static Task ResetForTesting()
        {
            return DisposeAllAsync(NullLogger.Instance);
        }

        /// <summary>
        /// Connects a <see cref="ConnectionMultiplexer"/> after ensuring the thread pool has a sane
        /// minimum size. StackExchange.Redis completes async commands on the .NET thread pool; the
        /// pool's default minimum equals the processor count, and once busy threads exceed that
        /// minimum the CLR only adds roughly one thread every 500ms. On small, busy hosts (a CI
        /// runner hosting the API + Postgres + Redis, or several integration-test assemblies running
        /// at once) that ramp-up stall can leave a Redis reply sitting unread in the socket past the
        /// 5s command timeout, surfacing as a spurious <c>RedisTimeoutException</c> whose diagnostics
        /// show <c>WORKER: (Busy=N, Min=processorCount)</c>. Raising the floor removes the stall.
        /// <see cref="Math.Max(int, int)"/> guarantees we never lower a host that already runs higher.
        /// </summary>
        private static ConnectionMultiplexer ConnectMultiplexer(string connectionString)
        {
            ThreadPool.GetMinThreads(out var workerThreads, out var completionPortThreads);
            ThreadPool.SetMinThreads(
                Math.Max(workerThreads, MinThreadPoolThreads),
                Math.Max(completionPortThreads, MinThreadPoolThreads));

            return ConnectionMultiplexer.Connect(connectionString);
        }

        // A discarded loser's connection failing to close cleanly doesn't affect the winner already published
        // for other callers, but it's still worth surfacing rather than swallowing outright.
        private static void DiscardLoser(IConnectionMultiplexer multiplexer, ILogger logger)
        {
            _ = DiscardLoserAsync(multiplexer, logger);
        }

        // Generic (rather than IConnectionMultiplexer-specific) so the dispose/log branch is unit-testable with a
        // fake IAsyncDisposable, mirroring DisposeAllAsync<T> above.
        internal static async Task DiscardLoserAsync<T>(T disposable, ILogger logger)
            where T : IAsyncDisposable
        {
            try
            {
                await disposable.DisposeAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to close a duplicate Redis multiplexer created while racing another connect for the same key.");
            }
        }

        internal readonly record struct ConnectFailure(DateTime OccurredAtUtc, Exception Error);
    }
}
