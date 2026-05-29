using Game.Infrastructure.Redis;
using Xunit;

namespace Game.TestInfrastructure.Fixtures
{
    public class IntegrationTestContainers : IAsyncLifetime
    {
        private readonly PostgresContainerFixture _postgres = new();
        private readonly RedisContainerFixture _redis = new();

        public string DbConnectionString => _postgres.ConnectionString;
        public string CacheConnectionString => _redis.ConnectionString;
        public string PubSubConnectionString => _redis.ConnectionString;

        public async ValueTask InitializeAsync()
        {
            RedisMultiplexerFactory.ResetForTesting();
            await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync());
        }

        public async ValueTask DisposeAsync()
        {
            await _postgres.DisposeAsync();
            await _redis.DisposeAsync();
        }
    }
}
