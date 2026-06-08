using Game.Api.Models.Enemies;
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
    public class ChallengeBossSocketTests : ApiIntegrationTestBase
    {
        public ChallengeBossSocketTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper) : base(containers, testOutputHelper) { }

        private async Task<(int UserId, int BossZoneId, int BosslessZoneId, int BossId)> SeedBossDataAsync()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            // A dedicated boss with several skills (so the full-loadout behaviour is observable).
            var boss = await TestDataSeeder.CreateEnemyAsync(context, "Catacomb Lich", isBoss: true);
            for (var i = 0; i < 5; i++)
            {
                var bossSkill = await TestDataSeeder.CreateSkillAsync(context, name: $"BossSkill{i}");
                await TestDataSeeder.LinkSkillToEnemyAsync(context, boss.Id, bossSkill.Id);
            }

            var bossZone = await TestDataSeeder.CreateZoneAsync(
                context, "Forgotten Catacombs", bossEnemyId: boss.Id, bossLevel: 18);
            var bosslessZone = await TestDataSeeder.CreateZoneAsync(context, "Bossless Glade");

            var playerSkill = await TestDataSeeder.CreateSkillAsync(context, name: "PlayerSkill");
            var user = await TestDataSeeder.CreateUserAsync(context, "bossuser", "bosspass");
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id, zoneId: bossZone.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, playerSkill.Id);

            var loginResponse = await Client.PostAsJsonAsync("/api/Login",
                new { Username = "bossuser", Password = "bosspass" });
            Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

            return (user.Id, bossZone.Id, bosslessZone.Id, boss.Id);
        }

        [Fact]
        public async Task ChallengeBoss_ZoneWithBoss_ReturnsBossEnemyInstance()
        {
            var (userId, bossZoneId, _, bossId) = await SeedBossDataAsync();

            await using var socketClient = new TestSocketClient();
            var wsClient = Factory.Server.CreateWebSocketClient();
            await socketClient.ConnectAsync(wsClient, userId);
            Assert.Equal(WebSocketState.Open, socketClient.State);

            var response = await socketClient.SendCommandAsync<NewEnemyModel>(
                "ChallengeBoss", new { ZoneId = (int?)bossZoneId });

            Assert.Null(response.Error);
            Assert.NotNull(response.Data);
            Assert.NotNull(response.Data.EnemyInstance);
            Assert.Equal(bossId, response.Data.EnemyInstance.Id);
            // Deterministic: fought at the authored boss level with its full (5-skill) loadout.
            Assert.Equal(18, response.Data.EnemyInstance.Level);
            Assert.Equal(5, response.Data.EnemyInstance.SelectedSkills.Count);
            Assert.True(response.Data.EnemyInstance.Seed > 0);
        }

        [Fact]
        public async Task ChallengeBoss_DefaultsToPlayerCurrentZone()
        {
            // The seeded player starts in the boss zone, so omitting ZoneId still challenges its boss.
            var (userId, _, _, bossId) = await SeedBossDataAsync();

            await using var socketClient = new TestSocketClient();
            var wsClient = Factory.Server.CreateWebSocketClient();
            await socketClient.ConnectAsync(wsClient, userId);

            var response = await socketClient.SendCommandAsync<NewEnemyModel>(
                "ChallengeBoss", new { ZoneId = (int?)null });

            Assert.Null(response.Error);
            Assert.NotNull(response.Data);
            Assert.NotNull(response.Data.EnemyInstance);
            Assert.Equal(bossId, response.Data.EnemyInstance.Id);
        }

        [Fact]
        public async Task ChallengeBoss_ZoneWithoutBoss_ReturnsError()
        {
            var (userId, _, bosslessZoneId, _) = await SeedBossDataAsync();

            await using var socketClient = new TestSocketClient();
            var wsClient = Factory.Server.CreateWebSocketClient();
            await socketClient.ConnectAsync(wsClient, userId);

            var response = await socketClient.SendCommandAsync<NewEnemyModel>(
                "ChallengeBoss", new { ZoneId = (int?)bosslessZoneId });

            Assert.NotNull(response.Error);
            Assert.Null(response.Data?.EnemyInstance);
        }
    }
}
