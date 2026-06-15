using Game.Api;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
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

            await Assert.ThrowsAnyAsync<Exception>(
                () => wsClient.ConnectAsync(new Uri("ws://localhost/socket"), CancellationToken));
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
    }
}
