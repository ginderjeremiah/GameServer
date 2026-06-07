using Game.Abstractions.DataAccess;
using Game.Api.Models.Auth;
using Game.Api.Models.Common;
using Game.Api.Services;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Base;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace Game.Api.Tests.Integration
{
    public abstract class ApiIntegrationTestBase : IAsyncLifetime
    {
        protected IntegrationTestContainers Containers { get; }
        protected GameServerFactory Factory { get; }
        protected HttpClient Client { get; private set; } = null!;

        protected static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

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
            scope.ServiceProvider.GetRequiredService<IChallenges>().InvalidateCache();
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await Factory.DisposeAsync();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Creates a new HttpClient carrying a bearer access token for the given user ID and any granted
        /// roles, with a player session pre-created in the cache.
        /// </summary>
        protected HttpClient CreateAuthenticatedClient(int userId, int playerId, params string[] roles)
        {
            var client = Factory.CreateClient();
            TestAuthHelper.AddAuthHeader(client, userId, roles);
            using var scope = Factory.Services.CreateScope();
            var sessionService = scope.ServiceProvider.GetRequiredService<SessionService>();
            sessionService.CreateSession(userId, playerId);
            return client;
        }

        /// <summary>
        /// Logs in with the given credentials through the real login flow and returns a client carrying
        /// the issued bearer access token, along with the full login result (tokens + player data).
        /// </summary>
        protected async Task<(HttpClient Client, LoginResult Login)> LoginAndBuildClientAsync(string username, string password)
        {
            var login = await LoginAsync(username, password);
            var client = Factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Tokens.AccessToken);
            return (client, login);
        }

        /// <summary>
        /// Logs in through the real login endpoint and returns the deserialized login result.
        /// </summary>
        protected async Task<LoginResult> LoginAsync(string username, string password)
        {
            var response = await Client.PostAsJsonAsync("/api/Login", new { Username = username, Password = password }, CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResult>>(CancellationToken);
            Assert.NotNull(result);
            Assert.NotNull(result.Data);
            return result.Data;
        }

        /// <summary>
        /// Creates a scoped service provider for direct DB access in test setup.
        /// </summary>
        protected IServiceScope CreateScope() => Factory.Services.CreateScope();
    }
}
