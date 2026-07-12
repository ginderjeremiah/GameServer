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
    public class NewEnemySocketTests : ApiIntegrationTestBase
    {
        public NewEnemySocketTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper) : base(containers, testOutputHelper) { }

        private async Task SetPlayerState(int userId, Action<PlayerState> modifyState)
        {
            using var scope = CreateScope();
            var sessionService = scope.ServiceProvider.GetRequiredService<SessionService>();
            sessionService.SetAuthenticatedUser(userId);
            await sessionService.LoadPlayerState();
            modifyState(sessionService.PlayerState);
            await sessionService.SavePlayerStateAsync();
        }

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

        [Fact]
        public async Task NewEnemy_AbandonResolvesOutcome_BundlesTheIncurredCooldownWithTheReplacementEnemy()
        {
            var (userId, _, _, _) = await SeedBattleDataAsync();

            var wsClient = Factory.Server.CreateWebSocketClient();

            await using (var socketClient1 = new TestSocketClient())
            {
                await socketClient1.ConnectAsync(wsClient, userId);

                var newEnemyResponse = await socketClient1.SendCommandAsync<NewEnemyModel>(
                    "NewEnemy", new { NewZoneId = (int?)null });
                Assert.Null(newEnemyResponse.Error);
                Assert.NotNull(newEnemyResponse.Data?.EnemyInstance);

                await socketClient1.CloseAsync();
            }

            // Backdate the in-flight battle so the next NewEnemy's abandon (#1851) resolves a real outcome
            // instead of handing it back still in progress (#1595) — mirrors BattleLost_ValidLoss_Succeeds.
            await SetPlayerState(userId, state =>
            {
                state.BattleStartTime = DateTime.UtcNow.AddMinutes(-30);
            });

            await using var socketClient = new TestSocketClient();
            await socketClient.ConnectAsync(wsClient, userId);

            // Regression coverage for #1881: a client that never sends DefeatEnemy/BattleLost and instead
            // loops NewEnemy (exactly what an idle loss/draw does) must get the just-incurred cooldown on the
            // SAME response as the fresh enemy, not a fresh enemy silently anchored ahead of it.
            var response = await socketClient.SendCommandAsync<NewEnemyModel>(
                "NewEnemy", new { NewZoneId = (int?)null });

            Assert.Null(response.Error);
            Assert.NotNull(response.Data);
            Assert.NotNull(response.Data.EnemyInstance);
            Assert.NotNull(response.Data.Cooldown);
            Assert.True(response.Data.Cooldown is > 0 and <= 5000);
        }
    }
}
