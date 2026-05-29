using Game.Api.Models.Enemies;
using Game.Api.Services;
using Game.Core.Players;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using Xunit;

namespace Game.Api.Tests.Integration
{
    [Collection("Integration")]
    public class DefeatEnemySocketTests : ApiIntegrationTestBase
    {
        public DefeatEnemySocketTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper) : base(containers, testOutputHelper) { }

        private async Task<(int userId, int playerId)> SeedAndLoginAsync()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var skill = await TestDataSeeder.CreateSkillAsync(context);
            var enemy = await TestDataSeeder.CreateEnemyAsync(context);
            await TestDataSeeder.LinkSkillToEnemyAsync(context, enemy.Id, skill.Id);
            var zone = await TestDataSeeder.CreateZoneAsync(context);
            await TestDataSeeder.LinkEnemyToZoneAsync(context, zone.Id, enemy.Id);

            var user = await TestDataSeeder.CreateUserAsync(context, "defeatuser", "defeatpass");
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id, zoneId: zone.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, skill.Id);

            var loginResponse = await Client.PostAsJsonAsync("/api/Login",
                new { Username = "defeatuser", Password = "defeatpass" });
            Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

            return (user.Id, player.Id);
        }

        private async Task SetPlayerState(int userId, int playerId, Action<PlayerState> modifyState)
        {
            using var scope = CreateScope();
            var sessionService = scope.ServiceProvider.GetRequiredService<SessionService>();
            await sessionService.LoadPlayerState(userId);
            modifyState(sessionService.PlayerState);
            sessionService.SavePlayerState();
        }

        [Fact]
        public async Task DefeatEnemy_ValidTimestamp_ReturnsRewards()
        {
            var (userId, playerId) = await SeedAndLoginAsync();

            var wsClient = Factory.Server.CreateWebSocketClient();

            await using (var socketClient1 = new TestSocketClient())
            {
                await socketClient1.ConnectAsync(wsClient, userId);
                Assert.Equal(WebSocketState.Open, socketClient1.State);

                // Start battle to update PlayerState with ActiveEnemy info
                var newEnemyResponse = await socketClient1.SendCommandAsync<NewEnemyModel>(
                    "NewEnemy", new { NewZoneId = (int?)null });

                Assert.Null(newEnemyResponse.Error);

                await socketClient1.CloseAsync();
            }

            // Update start time and reconnet socket to ensure DefeatEnemy uses the modified PlayerState
            await SetPlayerState(userId, playerId, state =>
            {
                state.SetActiveBattle(
                    state.ActiveEnemyId!.Value,
                    state.ActiveEnemyLevel!.Value,
                    state.BattleSeed!.Value,
                    startTime: DateTime.UtcNow.AddMinutes(-30), // pretend battle started 30 minutes ago
                    state.Snapshot!
                );
            });

            await using var socketClient = new TestSocketClient();
            await socketClient.ConnectAsync(wsClient, userId);
            Assert.Equal(WebSocketState.Open, socketClient.State);

            // Now defeat enemy with a timestamp that is sufficiently after the start to ensure victory
            var futureTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var response = await socketClient.SendCommandAsync<DefeatEnemyResponse>(
                "DefeatEnemy", new { Timestamp = futureTimestamp });

            Assert.Null(response.Error);
            Assert.NotNull(response.Data);
            Assert.NotNull(response.Data.Rewards);
            Assert.True(response.Data.Rewards.ExpReward >= 0);
        }

        [Fact]
        public async Task DefeatEnemy_NoActiveBattle_ReturnsError()
        {
            var (userId, _) = await SeedAndLoginAsync();

            await using var socketClient = new TestSocketClient();
            var wsClient = Factory.Server.CreateWebSocketClient();
            await socketClient.ConnectAsync(wsClient, userId);

            var futureTimestamp = DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeMilliseconds();
            var response = await socketClient.SendCommandAsync<DefeatEnemyResponse>(
                "DefeatEnemy", new { Timestamp = futureTimestamp });

            Assert.NotNull(response.Error);
        }

        [Fact]
        public async Task DefeatEnemy_TimestampTooEarly_ReturnsError()
        {
            var (userId, _) = await SeedAndLoginAsync();

            await using var socketClient = new TestSocketClient();
            var wsClient = Factory.Server.CreateWebSocketClient();
            await socketClient.ConnectAsync(wsClient, userId);

            // Start battle
            var newEnemyResponse = await socketClient.SendCommandAsync<NewEnemyModel>(
                "NewEnemy", new { NewZoneId = (int?)null });
            Assert.Null(newEnemyResponse.Error);

            // Try to defeat immediately (timestamp = now, not enough time to win)
            var nowTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var response = await socketClient.SendCommandAsync<DefeatEnemyResponse>(
                "DefeatEnemy", new { Timestamp = nowTimestamp });

            Assert.NotNull(response.Error);
        }
    }
}
