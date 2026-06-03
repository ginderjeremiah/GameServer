using Testcontainers.Redis;

namespace Game.TestInfrastructure.Fixtures
{
    public class RedisContainerFixture : IAsyncDisposable
    {
        private readonly PreexistingContainerInfo? _preexisting = PreexistingContainerInfo.TryLoad();

        // Only provision a Testcontainers-managed container when no pre-existing Redis was
        // supplied by the session-start hook (see PreexistingContainerInfo).
        private readonly RedisContainer? _container;

        public RedisContainerFixture()
        {
            _container = _preexisting is null
                ? new RedisBuilder("redis:7-alpine").Build()
                : null;
        }

        public string ConnectionString => _preexisting?.Redis ?? _container!.GetConnectionString();

        public async Task StartAsync()
        {
            if (_container is not null)
            {
                await _container.StartAsync();
            }
        }

        public async ValueTask DisposeAsync()
        {
            // A reused container is owned by the session-start hook, not the test process.
            if (_container is not null)
            {
                await _container.DisposeAsync();
            }

            GC.SuppressFinalize(this);
        }
    }
}
