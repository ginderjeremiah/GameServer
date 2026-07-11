using Game.Api.Models.Enemies;
using Game.Api.Services;
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
    public class NewEnemySocketTests : ApiIntegrationTestBase
    {
        public NewEnemySocketTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper) : base(containers, testOutputHelper) { }

        private async Task<(int UserId, int PlayerId, int ZoneId, int Zone2Id)> SeedBattleDataAsync()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var skill = await TestDataSeeder.CreateSkillAsync(context);
            var enemy = await TestDataSeeder.CreateEnemyAsync(context);
            await TestDataSeeder.LinkSkillToEnemyAsync(context, enemy.Id, skill.Id);
            var zone = await TestDataSeeder.CreateZoneAsync(context, "Zone 1");
            var zone2 = await TestDataSeeder.CreateZoneAsync(context, "Zone 2");
            await TestDataSeeder.LinkEnemyToZoneAsync(context, zone.Id, enemy.Id);
            await TestDataSeeder.LinkEnemyToZoneAsync(context, zone2.Id, enemy.Id);

            var user = await TestDataSeeder.CreateUserAsync(context, "battleuser", "battlepass");
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id, zoneId: zone.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, skill.Id);

            // Reload the caches so battle setup resolves the seeded enemy/zone (the caches no longer lazily refill).
            await ReloadReferenceCachesAsync();

            // Login to create session
            var loginResponse = await Client.PostAsJsonAsync("/api/Login",
                new { Username = "battleuser", Password = "battlepass" });
            Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

            return (user.Id, player.Id, zone.Id, zone2.Id);
        }

        [Fact]
        public async Task NewEnemy_ValidZone_ReturnsEnemyInstance()
        {
            var (userId, _, _, _) = await SeedBattleDataAsync();

            await using var socketClient = new TestSocketClient();
            var wsClient = Factory.Server.CreateWebSocketClient();
            await socketClient.ConnectAsync(wsClient, userId);
            Assert.Equal(WebSocketState.Open, socketClient.State);

            var response = await socketClient.SendCommandAsync<NewEnemyModel>(
                "NewEnemy", new { NewZoneId = (int?)null });

            Assert.Null(response.Error);
            Assert.NotNull(response.Data);
            Assert.NotNull(response.Data.EnemyInstance);
            Assert.True(response.Data.EnemyInstance.Seed > 0);
        }

        [Fact]
        public async Task NewEnemy_WithNewZoneId_Succeeds()
        {
            var (userId, _, _, zone2Id) = await SeedBattleDataAsync();

            await using var socketClient = new TestSocketClient();
            var wsClient = Factory.Server.CreateWebSocketClient();
            await socketClient.ConnectAsync(wsClient, userId);

            var response = await socketClient.SendCommandAsync<NewEnemyModel>(
                "NewEnemy", new { NewZoneId = zone2Id });

            Assert.Null(response.Error);
            Assert.NotNull(response.Data);
            Assert.NotNull(response.Data.EnemyInstance);
        }

        [Fact]
        public async Task NewEnemy_OnCooldown_ReturnsCooldown()
        {
            var (userId, playerId, _, _) = await SeedBattleDataAsync();

            using var scope = CreateScope();
            var sessionService = scope.ServiceProvider.GetRequiredService<SessionService>();
            await sessionService.CreateSession(userId, playerId);
            sessionService.PlayerState.SetCooldown(DateTime.UtcNow.AddMinutes(5)); // Set cooldown for 5 minutes
            await sessionService.SavePlayerStateAsync();

            await using var socketClient = new TestSocketClient();
            var wsClient = Factory.Server.CreateWebSocketClient();
            await socketClient.ConnectAsync(wsClient, userId);

            var response = await socketClient.SendCommandAsync<NewEnemyModel>(
                "NewEnemy", new { NewZoneId = (int?)null });

            Assert.Null(response.Error);
            Assert.NotNull(response.Data);
            Assert.NotNull(response.Data.Cooldown);
            Assert.True(response.Data.Cooldown > 0);
        }
    }
}
