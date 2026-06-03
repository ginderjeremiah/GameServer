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

        public string DbConnectionString => _postgres.ConnectionString;
        public string CacheConnectionString => _redis.ConnectionString;
        public string PubSubConnectionString => _redis.ConnectionString;

        private static string SuiteLockPath => Path.Combine(Path.GetTempPath(), "gameserver-integration-tests.lock");

        public async ValueTask InitializeAsync()
        {
            if (_reuseMode)
            {
                _suiteLock = await CrossProcessLock.AcquireAsync(SuiteLockPath);
            }

            RedisMultiplexerFactory.ResetForTesting();
            await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync());
        }

        public async ValueTask DisposeAsync()
        {
            await _postgres.DisposeAsync();
            await _redis.DisposeAsync();
            _suiteLock?.Dispose();
        }
    }
}
