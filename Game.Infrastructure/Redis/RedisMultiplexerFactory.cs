using Game.Infrastructure.Cache;
using Game.Infrastructure.PubSub;
using StackExchange.Redis;

namespace Game.Infrastructure.Redis
{
    internal static class RedisMultiplexerFactory
    {
        private static ConnectionMultiplexer? _cacheInstance;
        private static ConnectionMultiplexer? _pubsubInstance;
        private static readonly object _cacheLock = new();

        // Minimum size to grow the thread pool to before connecting (see ConnectMultiplexer).
        private const int MinThreadPoolThreads = 32;

        public static ConnectionMultiplexer GetMultiplexer(ICacheOptions config)
        {
            if (config.CacheConnectionString is null)
            {
                throw new InvalidOperationException($"{nameof(config.CacheConnectionString)} cannot be null.");
            }
            else if (_cacheInstance is null)
            {
                lock (_cacheLock)
                {
                    if (_pubsubInstance is not null && _pubsubInstance.Configuration == config.CacheConnectionString)
                    {
                        _cacheInstance = _pubsubInstance;
                    }
                    else
                    {
                        _cacheInstance ??= ConnectMultiplexer(config.CacheConnectionString);
                    }
                }
            }

            return _cacheInstance;
        }

        internal static void ResetForTesting()
        {
            lock (_cacheLock)
            {
                _cacheInstance?.Dispose();
                _cacheInstance = null;
                _pubsubInstance?.Dispose();
                _pubsubInstance = null;
            }
        }

        public static ConnectionMultiplexer GetMultiplexer(IPubSubOptions config)
        {
            if (config.PubSubConnectionString is null)
            {
                throw new InvalidOperationException($"{nameof(config.PubSubConnectionString)} cannot be null.");
            }

            if (_pubsubInstance is null)
            {
                lock (_cacheLock)
                {
                    if (_cacheInstance is not null && _cacheInstance.Configuration == config.PubSubConnectionString)
                    {
                        _pubsubInstance = _cacheInstance;
                    }
                    else
                    {
                        _pubsubInstance ??= ConnectMultiplexer(config.PubSubConnectionString);
                    }
                }
            }

            return _pubsubInstance;
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
