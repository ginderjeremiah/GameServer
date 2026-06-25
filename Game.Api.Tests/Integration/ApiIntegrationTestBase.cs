using Game.Abstractions.DataAccess;
using Game.Api.Models.Auth;
using Game.Api.Models.Common;
using Game.Api.Models.Player;
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
            Factory = CreateFactory(containers, testOutputHelper);
        }

        protected virtual GameServerFactory CreateFactory(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
        {
            return new GameServerFactory(containers, testOutputHelper);
        }

        public async ValueTask InitializeAsync()
        {
            Client = Factory.CreateClient();

            using var scope = Factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            await DatabaseCleaner.TruncatePlayerDataAsync(context);
            await RedisCleaner.FlushAsync(Containers.CacheConnectionString);
            await ReferenceCacheReloader.ReloadAllAsync(scope.ServiceProvider);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await Factory.DisposeAsync();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Creates a new HttpClient carrying a post-selection bearer access token (the selected-player claim
        /// set to <paramref name="playerId"/>) for the given user ID and any granted roles, with a player
        /// session pre-created in the cache.
        /// </summary>
        protected HttpClient CreateAuthenticatedClient(int userId, int playerId, params string[] roles)
        {
            var client = Factory.CreateClient();
            TestAuthHelper.AddAuthHeader(client, userId, playerId, roles);
            using var scope = Factory.Services.CreateScope();
            var sessionService = scope.ServiceProvider.GetRequiredService<SessionService>();
            sessionService.CreateSession(userId, playerId);
            return client;
        }

        /// <summary>
        /// Logs in and selects the account's first character through the real auth flow, returning a
        /// game-ready client carrying the post-selection bearer access token (selected-player claim set)
        /// along with the rotated token pair.
        /// </summary>
        protected async Task<(HttpClient Client, AuthTokens Tokens)> LoginAndBuildClientAsync(string username, string password)
        {
            var login = await LoginAsync(username, password);
            var select = await SelectPlayerAsync(login.Tokens, login.PlayerSummaries[0].Id);
            var client = Factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", select.Tokens.AccessToken);
            return (client, select.Tokens);
        }

        /// <summary>
        /// Logs in through the real login endpoint and returns the deserialized login result (pre-selection
        /// tokens plus the account's player summaries).
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
        /// Selects a character through the real SelectPlayer endpoint (authenticated with the pre-selection
        /// access token, rotating the supplied refresh token) and returns the deserialized result (rotated
        /// tokens plus the loaded player).
        /// </summary>
        protected async Task<SelectPlayerResult> SelectPlayerAsync(AuthTokens tokens, int playerId)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/Login/SelectPlayer")
            {
                Content = JsonContent.Create(new { PlayerId = playerId, tokens.RefreshToken }),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

            var response = await Client.SendAsync(request, CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<SelectPlayerResult>>(CancellationToken);
            Assert.NotNull(result);
            Assert.NotNull(result.Data);
            return result.Data;
        }

        /// <summary>
        /// Creates a scoped service provider for direct DB access in test setup.
        /// </summary>
        protected IServiceScope CreateScope() => Factory.Services.CreateScope();

        /// <summary>
        /// Reads the persisted player snapshot directly from the write-behind cache (cache-first, with a DB
        /// miss-reload) and projects it the same way the API does. Socket-write tests poll this to confirm a
        /// fire-and-forget cache write has landed.
        /// </summary>
        protected async Task<PlayerData> GetPersistedPlayerAsync(int playerId)
        {
            using var scope = CreateScope();
            var playerRepo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            var player = await playerRepo.GetPlayer(playerId);
            Assert.NotNull(player);
            return PlayerData.FromPlayer(player, []);
        }

        /// <summary>
        /// Rebuilds the reference-data cache snapshots from the current database state. Call after seeding
        /// reference rows directly so the caches (which no longer lazily refill) serve the seeded data.
        /// </summary>
        protected Task ReloadReferenceCachesAsync() => ReferenceCacheReloader.ReloadAllAsync(Factory.Services);
    }
}
