using Game.Api.Models.Progress;
using Game.Core;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.Extensions.DependencyInjection;
using System.Net.WebSockets;
using Xunit;

namespace Game.Api.Tests.Integration
{
    /// <summary>
    /// Integration tests for the player-scoped <c>GetPlayerStatistics</c> socket command (#563),
    /// the WebSocket replacement for the <c>GET /api/Statistics</c> read.
    /// </summary>
    [Collection("Integration")]
    public class GetPlayerStatisticsSocketTests : ApiIntegrationTestBase
    {
        public GetPlayerStatisticsSocketTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper) : base(containers, testOutputHelper) { }

        private async Task<TestSocketClient> ConnectAsync(int userId)
        {
            var socketClient = new TestSocketClient();
            var wsClient = Factory.Server.CreateWebSocketClient();
            await socketClient.ConnectAsync(wsClient, userId);
            Assert.Equal(WebSocketState.Open, socketClient.State);
            return socketClient;
        }

        [Fact]
        public async Task GetPlayerStatistics_WithSeededStatistics_ReturnsThisPlayersStatistics()
        {
            int userId;
            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();

                var user = await TestDataSeeder.CreateUserAsync(context, "playerstatsuser", "playerstatspass");
                var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
                userId = user.Id;

                await TestDataSeeder.AddPlayerStatisticAsync(context, player.Id, EStatisticType.EnemiesKilled, 7m);
                await TestDataSeeder.AddPlayerStatisticAsync(context, player.Id, EStatisticType.EnemiesKilled, 3m, entityId: 1);

                // A second player with different stats: the command must resolve the player from the socket
                // session, so none of these may leak into the connected player's response.
                var otherUser = await TestDataSeeder.CreateUserAsync(context, "otherstatsuser", "otherstatspass");
                var otherPlayer = await TestDataSeeder.CreatePlayerAsync(context, otherUser.Id);
                await TestDataSeeder.AddPlayerStatisticAsync(context, otherPlayer.Id, EStatisticType.BattlesWon, 99m);
            }

            // Login creates the Redis session the socket handshake requires.
            await LoginAsync("playerstatsuser", "playerstatspass");
            await using var socketClient = await ConnectAsync(userId);

            var response = await socketClient.SendCommandAsync<List<PlayerStatistic>>("GetPlayerStatistics");

            Assert.Null(response.Error);
            Assert.NotNull(response.Data);
            Assert.Equal(2, response.Data.Count);

            var total = Assert.Single(response.Data, s => s.StatisticTypeId == EStatisticType.EnemiesKilled && s.EntityId is null);
            Assert.Equal(7m, total.Value);

            var perEnemy = Assert.Single(response.Data, s => s.StatisticTypeId == EStatisticType.EnemiesKilled && s.EntityId == 1);
            Assert.Equal(3m, perEnemy.Value);

            // The other player's stat must not bleed through.
            Assert.DoesNotContain(response.Data, s => s.StatisticTypeId == EStatisticType.BattlesWon);
        }

        [Fact]
        public async Task GetPlayerStatistics_NewPlayer_ReturnsEmptyWithoutError()
        {
            int userId;
            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                var user = await TestDataSeeder.CreateUserAsync(context, "newstatsuser", "newstatspass");
                await TestDataSeeder.CreatePlayerAsync(context, user.Id);
                userId = user.Id;
            }

            await LoginAsync("newstatsuser", "newstatspass");
            await using var socketClient = await ConnectAsync(userId);

            var response = await socketClient.SendCommandAsync<List<PlayerStatistic>>("GetPlayerStatistics");

            Assert.Null(response.Error);
            Assert.NotNull(response.Data);
            Assert.Empty(response.Data);
        }
    }
}
