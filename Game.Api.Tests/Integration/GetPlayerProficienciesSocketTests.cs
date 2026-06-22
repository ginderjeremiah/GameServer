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
    /// Integration tests for the player-scoped <c>GetPlayerProficiencies</c> socket command (#1115): the
    /// WebSocket read of a player's proficiency progress (level + XP per proficiency).
    /// </summary>
    [Collection("Integration")]
    public class GetPlayerProficienciesSocketTests : ApiIntegrationTestBase
    {
        public GetPlayerProficienciesSocketTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper) : base(containers, testOutputHelper) { }

        private async Task<TestSocketClient> ConnectAsync(int userId)
        {
            var socketClient = new TestSocketClient();
            var wsClient = Factory.Server.CreateWebSocketClient();
            await socketClient.ConnectAsync(wsClient, userId);
            Assert.Equal(WebSocketState.Open, socketClient.State);
            return socketClient;
        }

        [Fact]
        public async Task GetPlayerProficiencies_WithSeededProgress_ReturnsThisPlayersProficiencies()
        {
            int userId;
            int proficiencyId;
            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();

                var user = await TestDataSeeder.CreateUserAsync(context, "profsockuser", "profsockpass");
                var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
                userId = user.Id;

                var proficiency = await TestDataSeeder.CreateProficiencyAsync(context);
                proficiencyId = proficiency.Id;
                await TestDataSeeder.AddPlayerProficiencyAsync(context, player.Id, proficiencyId, level: 2, xp: 130m);

                // A second player with their own progress: the command must resolve the player from the
                // socket session, so this must not leak into the connected player's response.
                var otherUser = await TestDataSeeder.CreateUserAsync(context, "otherprofsockuser", "otherprofsockpass");
                var otherPlayer = await TestDataSeeder.CreatePlayerAsync(context, otherUser.Id);
                var otherProficiency = await TestDataSeeder.CreateProficiencyAsync(context, "Other Proficiency");
                await TestDataSeeder.AddPlayerProficiencyAsync(context, otherPlayer.Id, otherProficiency.Id, level: 5, xp: 999m);
            }

            // Login creates the Redis session the socket handshake requires.
            await LoginAsync("profsockuser", "profsockpass");
            await using var socketClient = await ConnectAsync(userId);

            var response = await socketClient.SendCommandAsync<List<PlayerProficiency>>("GetPlayerProficiencies");

            Assert.Null(response.Error);
            Assert.NotNull(response.Data);

            var playerProficiency = Assert.Single(response.Data);
            Assert.Equal(proficiencyId, playerProficiency.ProficiencyId);
            Assert.Equal(2, playerProficiency.Level);
            Assert.Equal(130m, playerProficiency.Xp);
        }

        [Fact]
        public async Task GetPlayerProficiencies_NewPlayer_ReturnsEmptyWithoutError()
        {
            int userId;
            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                var user = await TestDataSeeder.CreateUserAsync(context, "newprofsockuser", "newprofsockpass");
                await TestDataSeeder.CreatePlayerAsync(context, user.Id);
                userId = user.Id;
            }

            await LoginAsync("newprofsockuser", "newprofsockpass");
            await using var socketClient = await ConnectAsync(userId);

            var response = await socketClient.SendCommandAsync<List<PlayerProficiency>>("GetPlayerProficiencies");

            Assert.Null(response.Error);
            Assert.NotNull(response.Data);
            Assert.Empty(response.Data);
        }
    }
}
