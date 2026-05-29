using Game.Abstractions.DataAccess;
using Game.Api.Services;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Base;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Game.Api.Tests.Integration
{
    public abstract class ApiIntegrationTestBase : IAsyncLifetime
    {
        protected IntegrationTestContainers Containers { get; }
        protected GameServerFactory Factory { get; }
        protected HttpClient Client { get; private set; } = null!;

        protected ApiIntegrationTestBase(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
        {
            Containers = containers;
            Factory = new GameServerFactory(containers, testOutputHelper);
        }

        public async ValueTask InitializeAsync()
        {
            Client = Factory.CreateClient();

            using var scope = Factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            await DatabaseCleaner.TruncatePlayerDataAsync(context);
            await RedisCleaner.FlushAsync(Containers.CacheConnectionString);

            // Invalidate static caches in repositories so stale data doesn't leak between tests
            scope.ServiceProvider.GetRequiredService<IEnemies>().InvalidateCache();
            scope.ServiceProvider.GetRequiredService<IZones>().InvalidateCache();
            scope.ServiceProvider.GetRequiredService<ISkills>().InvalidateCache();
            scope.ServiceProvider.GetRequiredService<IItems>().InvalidateCache();
            scope.ServiceProvider.GetRequiredService<IItemMods>().InvalidateCache();
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await Factory.DisposeAsync();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Creates a new HttpClient with an auth cookie for the given user ID.
        /// </summary>
        protected HttpClient CreateAuthenticatedClient(int userId, int playerId)
        {
            var client = Factory.CreateClient();
            TestAuthHelper.AddAuthCookie(client, userId);
            using var scope = Factory.Services.CreateScope();
            var sessionService = scope.ServiceProvider.GetRequiredService<SessionService>();
            sessionService.CreateSession(userId, playerId);
            return client;
        }

        /// <summary>
        /// Creates a scoped service provider for direct DB access in test setup.
        /// </summary>
        protected IServiceScope CreateScope() => Factory.Services.CreateScope();
    }
}
