using Game.Infrastructure.Redis;
using Xunit;

namespace Game.TestInfrastructure.Fixtures
{
    public class IntegrationTestContainers : IAsyncLifetime
    {
        private readonly PostgresContainerFixture _postgres = new();
        private readonly RedisContainerFixture _redis = new();

        // In reuse mode every test assembly shares one PostgreSQL database and one Redis instance,
        // so the integration suites must not run concurrently across assemblies — their per-test
        // truncate/flush cleanup would otherwise corrupt each other. A cross-process lock held for
        // the lifetime of this collection fixture serializes them. Under Testcontainers each
        // assembly owns its containers, so the lock is unnecessary.
        private readonly bool _reuseMode = PreexistingContainerInfo.TryLoad() is not null;
        private CrossProcessLock? _suiteLock;

        // Pre-warm a high thread-pool floor for the whole test process before any integration test
        // touches Redis or Postgres. The CI test-backend job runs every test assembly concurrently
        // under the dotnet-coverage profiler on a 2-core runner; under that load the .NET thread pool
        // (which dispatches the StackExchange.Redis and Npgsql I/O completions) was starving — it
        // grows only ~1 thread/500ms past its minimum, so a reply could sit unread past the 5s Redis
        // command timeout (or the 30s Npgsql read timeout), surfacing as an intermittent
        // RedisTimeoutException / Npgsql TimeoutException in whichever integration test was running at
        // the contention peak (#517). The production RedisMultiplexerFactory floor (32) proved too low
        // for this multi-assembly + coverage process (failure diagnostics: Busy=35, Min=32,
        // QueuedItems=36), and the test-only RedisCleaner connects outside that factory anyway, so the
        // floor is raised here — the per-assembly integration chokepoint. Math.Max never lowers a host
        // that already runs higher. Production code is untouched.
        private const int MinThreadPoolThreads = 200;

        public string DbConnectionString => _postgres.ConnectionString;
        public string CacheConnectionString => _redis.ConnectionString;
        public string PubSubConnectionString => _redis.ConnectionString;

        private static string SuiteLockPath => Path.Combine(Path.GetTempPath(), "gameserver-integration-tests.lock");

        public async ValueTask InitializeAsync()
        {
            EnsureThreadPoolFloor();

            if (_reuseMode)
            {
                _suiteLock = await CrossProcessLock.AcquireAsync(SuiteLockPath);
            }

            await RedisMultiplexerFactory.ResetForTesting();
            await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync());
        }

        private static void EnsureThreadPoolFloor()
        {
            ThreadPool.GetMinThreads(out var workerThreads, out var completionPortThreads);
            ThreadPool.SetMinThreads(
                Math.Max(workerThreads, MinThreadPoolThreads),
                Math.Max(completionPortThreads, MinThreadPoolThreads));
        }

        public async ValueTask DisposeAsync()
        {
            await _postgres.DisposeAsync();
            await _redis.DisposeAsync();
            _suiteLock?.Dispose();
        }
    }
}
