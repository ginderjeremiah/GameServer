using Game.Api.Models.Progress;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.Extensions.DependencyInjection;
using System.Net.WebSockets;
using Xunit;

namespace Game.Api.Tests.Integration
{
    /// <summary>
    /// Integration tests for the player-scoped <c>GetPlayerChallenges</c> socket command (#564),
    /// the WebSocket replacement for the <c>GET /api/Challenges/Player</c> read.
    /// </summary>
    [Collection("Integration")]
    public class GetPlayerChallengesSocketTests : ApiIntegrationTestBase
    {
        public GetPlayerChallengesSocketTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper) : base(containers, testOutputHelper) { }

        private async Task<TestSocketClient> ConnectAsync(int userId)
        {
            var socketClient = new TestSocketClient();
            var wsClient = Factory.Server.CreateWebSocketClient();
            await socketClient.ConnectAsync(wsClient, userId);
            Assert.Equal(WebSocketState.Open, socketClient.State);
            return socketClient;
        }

        [Fact]
        public async Task GetPlayerChallenges_WithSeededProgress_ReturnsThisPlayersChallenges()
        {
            int userId;
            int challengeId;
            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();

                var user = await TestDataSeeder.CreateUserAsync(context, "challsockuser", "challsockpass");
                var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
                userId = user.Id;

                var challenge = await TestDataSeeder.CreateChallengeAsync(context);
                challengeId = challenge.Id;
                await TestDataSeeder.AddPlayerChallengeAsync(
                    context, player.Id, challengeId, progress: 10m, completed: true, completedAt: DateTime.UtcNow);

                // A second player with their own progress: the command must resolve the player from the
                // socket session, so this must not leak into the connected player's response.
                var otherUser = await TestDataSeeder.CreateUserAsync(context, "otherchallsockuser", "otherchallsockpass");
                var otherPlayer = await TestDataSeeder.CreatePlayerAsync(context, otherUser.Id);
                var otherChallenge = await TestDataSeeder.CreateChallengeAsync(context);
                await TestDataSeeder.AddPlayerChallengeAsync(
                    context, otherPlayer.Id, otherChallenge.Id, progress: 5m, completed: false);
            }

            // Reload the caches so the newly seeded challenges are resolvable by id during the read (the
            // caches no longer lazily refill).
            await ReloadReferenceCachesAsync();

            // Login creates the Redis session the socket handshake requires.
            await LoginAsync("challsockuser", "challsockpass");
            await using var socketClient = await ConnectAsync(userId);

            var response = await socketClient.SendCommandAsync<List<PlayerChallenge>>("GetPlayerChallenges");

            Assert.Null(response.Error);
            Assert.NotNull(response.Data);

            var playerChallenge = Assert.Single(response.Data);
            Assert.Equal(challengeId, playerChallenge.ChallengeId);
            Assert.Equal(10m, playerChallenge.Progress);
            Assert.True(playerChallenge.Completed);
            Assert.NotNull(playerChallenge.CompletedAt);
        }

        [Fact]
        public async Task GetPlayerChallenges_NewPlayer_ReturnsEmptyWithoutError()
        {
            int userId;
            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                var user = await TestDataSeeder.CreateUserAsync(context, "newchallsockuser", "newchallsockpass");
                await TestDataSeeder.CreatePlayerAsync(context, user.Id);
                userId = user.Id;
            }

            await LoginAsync("newchallsockuser", "newchallsockpass");
            await using var socketClient = await ConnectAsync(userId);

            var response = await socketClient.SendCommandAsync<List<PlayerChallenge>>("GetPlayerChallenges");

            Assert.Null(response.Error);
            Assert.NotNull(response.Data);
            Assert.Empty(response.Data);
        }
    }
}
