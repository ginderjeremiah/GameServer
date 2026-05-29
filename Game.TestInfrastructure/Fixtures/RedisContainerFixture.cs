using Testcontainers.Redis;

namespace Game.TestInfrastructure.Fixtures
{
    public class RedisContainerFixture : IAsyncDisposable
    {
        private readonly RedisContainer _container = new RedisBuilder("redis:7-alpine").Build();

        public string ConnectionString => _container.GetConnectionString();

        public async Task StartAsync()
        {
            await _container.StartAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await _container.DisposeAsync();
            GC.SuppressFinalize(this);
        }
    }
}
