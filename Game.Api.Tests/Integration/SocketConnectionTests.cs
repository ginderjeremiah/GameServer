using Game.Api;
using Game.Api.Models.Common;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text.Json;
using Xunit;

namespace Game.Api.Tests.Integration
{
    [Collection("Integration")]
    public class SocketConnectionTests : ApiIntegrationTestBase
    {
        public SocketConnectionTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper) : base(containers, testOutputHelper) { }

        /// <summary>
        /// Seeds a user with a player (and a linked skill so the aggregate loads), without establishing a
        /// session in Redis. Returns the userId (for WebSocket auth) and playerId.
        /// </summary>
        private async Task<(int UserId, int PlayerId)> SeedAsync(string username = "socketuser", string password = "socketpass")
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var user = await TestDataSeeder.CreateUserAsync(context, username, password);
            var skill = await TestDataSeeder.CreateSkillAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, skill.Id);
            // The caches no longer lazily refill, so reload them to resolve the player's linked skill on load.
            await ReloadReferenceCachesAsync();

            return (user.Id, player.Id);
        }

        /// <summary>
        /// Seeds test data, logs in via HTTP (creating the session in Redis), and returns the userId (for
        /// WebSocket auth) and playerId.
        /// </summary>
        private async Task<(int UserId, int PlayerId)> SeedAndLoginAsync(string username = "socketuser", string password = "socketpass")
        {
            var seeded = await SeedAsync(username, password);

            var loginResponse = await Client.PostAsJsonAsync("/api/Login",
                new { Username = username, Password = password });
            Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

            return seeded;
        }

        /// <summary>
        /// Seeds a user with no player, so an authenticated handshake for it resolves no player to load.
        /// Returns the userId (for WebSocket auth).
        /// </summary>
        private async Task<int> SeedUserWithoutPlayerAsync(string username = "socketnoplayer", string password = "socketpass")
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var user = await TestDataSeeder.CreateUserAsync(context, username, password);
            return user.Id;
        }

        /// <summary>Reads the live TTL on the player's socket-presence key directly from Redis.</summary>
        private async Task<TimeSpan?> GetPresenceTtlAsync(int playerId)
        {
            await using var multiplexer = await ConnectionMultiplexer.ConnectAsync(Containers.CacheConnectionString);
            return await multiplexer.GetDatabase().KeyTimeToLiveAsync($"{Constants.CACHE_PLAYER_SOCKET_PREFIX}_{playerId}");
        }

        [Fact]
        public async Task Connect_Authenticated_Succeeds()
        {
            var (userId, _) = await SeedAndLoginAsync();

            await using var socketClient = new TestSocketClient();
            var wsClient = Factory.Server.CreateWebSocketClient();
            await socketClient.ConnectAsync(wsClient, userId);

            Assert.Equal(WebSocketState.Open, socketClient.State);
        }

        [Fact]
        public async Task Connect_Unauthenticated_Fails()
        {
            // A genuinely unauthenticated handshake — no access_token at all — is rejected. (A valid token
            // with no cached session is a different, authenticated case; see the rehydration test below.)
            var wsClient = Factory.Server.CreateWebSocketClient();

            // The TestServer client throws InvalidOperationException with the rejected status code baked
            // into the message when the handshake doesn't complete with a 101 — tightened from ThrowsAny so
            // this can't pass on, say, a 500 as readily as the intended 401-rejected upgrade.
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => wsClient.ConnectAsync(new Uri("ws://localhost/socket"), CancellationToken));
            Assert.Contains("401", ex.Message);
        }

        [Fact]
        public async Task Socket_Unauthenticated_ReturnsErrorEnvelope()
        {
            // A plain HTTP request to /socket with no token short-circuits to 401 before any upgrade, so the
            // body carries the project's { errorMessage } envelope rather than being empty.
            var response = await Client.GetAsync("/socket", CancellationToken);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.NotNull(body);
            Assert.False(string.IsNullOrWhiteSpace(body.ErrorMessage));
        }

        [Fact]
        public async Task Socket_AuthenticatedButNotWebSocketUpgrade_ReturnsErrorEnvelope()
        {
            // An authenticated but non-upgrade request to /socket short-circuits to 400 before any upgrade, so
            // the body carries the { errorMessage } envelope rather than being empty.
            var (userId, playerId) = await SeedAsync();
            var authedClient = await CreateAuthenticatedClient(userId, playerId);

            var response = await authedClient.GetAsync("/socket", CancellationToken);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.NotNull(body);
            Assert.False(string.IsNullOrWhiteSpace(body.ErrorMessage));
        }

        [Fact]
        public async Task Socket_AuthenticatedButPlayerNotLoadable_ReturnsErrorEnvelope()
        {
            // The player-load short-circuit sits after the WebSocket-upgrade check, so — unlike the 401/400
            // cases — it can't be reached with a plain GET (that stops at the 400 branch). The request is
            // driven through the pipeline presenting as a WebSocket upgrade (a stubbed feature), but the
            // socket is never accepted: the authenticated user has no player, so the handshake loads none and
            // short-circuits to 404 with the { errorMessage } envelope still written to the (un-upgraded) body.
            var userId = await SeedUserWithoutPlayerAsync();
            var token = TestAuthHelper.CreateAccessToken(userId);

            var context = await Factory.Server.SendAsync(ctx =>
            {
                ctx.Request.Method = HttpMethods.Get;
                ctx.Request.Path = "/socket";
                ctx.Request.QueryString = new QueryString($"?access_token={token}");
                ctx.Features.Set<IHttpWebSocketFeature>(new StubWebSocketFeature());
            }, CancellationToken);

            Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
            // The response body stream isn't seekable, so deserialize straight from its start.
            var body = await JsonSerializer.DeserializeAsync<ApiResponse>(
                context.Response.Body,
                new JsonSerializerOptions(JsonSerializerDefaults.Web),
                CancellationToken);
            Assert.NotNull(body);
            Assert.False(string.IsNullOrWhiteSpace(body.ErrorMessage));
        }

        [Fact]
        public async Task Connect_ValidTokenWithNoSessionCache_RehydratesAndSucceeds()
        {
            // A valid token whose session was never established in Redis (or was evicted) must connect by
            // rehydrating the session from the user's player binding, not be rejected as unauthenticated (#693).
            var (userId, _) = await SeedAsync();

            await using var socketClient = new TestSocketClient();
            var wsClient = Factory.Server.CreateWebSocketClient();
            await socketClient.ConnectAsync(wsClient, userId);

            Assert.Equal(WebSocketState.Open, socketClient.State);
        }

        [Fact]
        public async Task Connect_SetsPresenceKeyWithTtl()
        {
            var (userId, playerId) = await SeedAndLoginAsync();

            await using var socketClient = new TestSocketClient();
            var wsClient = Factory.Server.CreateWebSocketClient();
            await socketClient.ConnectAsync(wsClient, userId);
            // Round-trip a command so registration (which sets the presence key + TTL) has completed.
            await socketClient.SendCommandAsync<object>("GetStatisticTypes");

            var ttl = await GetPresenceTtlAsync(playerId);

            // The presence key carries a TTL, so a connection that vanishes without a clean close can't
            // leave the key lingering forever.
            Assert.NotNull(ttl);
            Assert.True(ttl > TimeSpan.Zero, $"Expected a positive TTL but got {ttl}.");
            Assert.True(ttl <= TimeSpan.FromSeconds(30), $"Expected TTL <= 30s but got {ttl}.");
        }

        [Fact]
        public async Task SocketActivity_RefreshesPresenceTtl()
        {
            var (userId, playerId) = await SeedAndLoginAsync();

            await using var socketClient = new TestSocketClient();
            var wsClient = Factory.Server.CreateWebSocketClient();
            await socketClient.ConnectAsync(wsClient, userId);
            await socketClient.SendCommandAsync<object>("GetStatisticTypes");

            var initial = await GetPresenceTtlAsync(playerId);

            // Let the TTL decay (the test client sends no heartbeat of its own), then prove a fresh
            // inbound message bumps it back up.
            await Task.Delay(2000, CancellationToken);
            var decayed = await GetPresenceTtlAsync(playerId);

            await socketClient.SendCommandAsync<object>("GetStatisticTypes");
            var refreshed = await GetPresenceTtlAsync(playerId);

            Assert.NotNull(initial);
            Assert.NotNull(decayed);
            Assert.NotNull(refreshed);
            Assert.True(decayed < initial, $"Expected the TTL to decay below {initial} but it was {decayed}.");
            Assert.True(refreshed > decayed, $"Expected activity to refresh the TTL above {decayed} but it was {refreshed}.");
        }

        /// <summary>
        /// Presents a request to the pipeline as a WebSocket upgrade so the interceptor reaches its
        /// post-upgrade checks, without ever completing a handshake. <see cref="AcceptAsync"/> is never
        /// invoked by tests that short-circuit before the upgrade, so it deliberately throws.
        /// </summary>
        private sealed class StubWebSocketFeature : IHttpWebSocketFeature
        {
            public bool IsWebSocketRequest => true;

            public Task<WebSocket> AcceptAsync(WebSocketAcceptContext context) =>
                throw new InvalidOperationException("The stub WebSocket feature never accepts a connection.");
        }
    }
}
