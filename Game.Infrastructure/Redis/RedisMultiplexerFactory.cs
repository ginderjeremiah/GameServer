using Game.Infrastructure.Cache;
using Game.Infrastructure.PubSub;
using StackExchange.Redis;

namespace Game.Infrastructure.Redis
{
    internal static class RedisMultiplexerFactory
    {
        // Multiplexers are keyed by the *original* connection string under a single lock. Keying by the raw
        // string (rather than comparing against ConnectionMultiplexer.Configuration, which StackExchange.Redis
        // normalizes/rewrites) makes reuse reliable: the cache and pub/sub services share one multiplexer
        // whenever their connection strings match, and two getters racing on first startup resolve to the same
        // instance instead of each opening their own (#696). The whole get-or-add runs under the lock, so a
        // partially-constructed multiplexer is never published to another thread.
        private static readonly Dictionary<string, ConnectionMultiplexer> _multiplexers = new();
        private static readonly object _lock = new();

        // Minimum size to grow the thread pool to before connecting (see ConnectMultiplexer).
        private const int MinThreadPoolThreads = 32;

        public static ConnectionMultiplexer GetMultiplexer(ICacheOptions config)
        {
            if (config.CacheConnectionString is null)
            {
                throw new InvalidOperationException($"{nameof(config.CacheConnectionString)} cannot be null.");
            }

            return GetOrConnect(config.CacheConnectionString);
        }

        public static ConnectionMultiplexer GetMultiplexer(IPubSubOptions config)
        {
            if (config.PubSubConnectionString is null)
            {
                throw new InvalidOperationException($"{nameof(config.PubSubConnectionString)} cannot be null.");
            }

            return GetOrConnect(config.PubSubConnectionString);
        }

        private static ConnectionMultiplexer GetOrConnect(string connectionString)
        {
            return GetOrAdd(_multiplexers, _lock, connectionString, ConnectMultiplexer);
        }

        /// <summary>
        /// Locked get-or-add: returns the value cached for <paramref name="key"/>, or invokes
        /// <paramref name="factory"/> under <paramref name="syncRoot"/> to create and cache one on first request.
        /// Running the whole lookup-and-create inside the lock is what makes the cache safe to read from multiple
        /// threads without a partially-constructed value escaping, and what collapses a first-startup race onto a
        /// single created instance. Generic over the value type so the keyed-reuse semantics are unit-testable
        /// without a live connection (#696); production keys <see cref="ConnectionMultiplexer"/> by connection
        /// string.
        /// </summary>
        internal static T GetOrAdd<T>(Dictionary<string, T> cache, object syncRoot, string key, Func<string, T> factory)
        {
            lock (syncRoot)
            {
                if (!cache.TryGetValue(key, out var value))
                {
                    value = factory(key);
                    cache[key] = value;
                }

                return value;
            }
        }

        internal static void ResetForTesting()
        {
            lock (_lock)
            {
                foreach (var multiplexer in _multiplexers.Values)
                {
                    multiplexer.Dispose();
                }

                _multiplexers.Clear();
            }
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
    }
}
