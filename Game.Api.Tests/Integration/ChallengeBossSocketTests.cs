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
    public class ChallengeBossSocketTests : ApiIntegrationTestBase
    {
        public ChallengeBossSocketTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper) : base(containers, testOutputHelper) { }

        private async Task SetPlayerState(int userId, Action<PlayerState> modifyState)
        {
            using var scope = CreateScope();
            var sessionService = scope.ServiceProvider.GetRequiredService<SessionService>();
            sessionService.SetAuthenticatedUser(userId);
            await sessionService.LoadPlayerState();
            modifyState(sessionService.PlayerState);
            await sessionService.SavePlayerStateAsync();
        }

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

            // Reload the caches so the boss battle resolves the seeded boss/zone (the caches no longer lazily refill).
            await ReloadReferenceCachesAsync();

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

        [Fact]
        public async Task ChallengeBoss_FirstChallenge_ReturnsNoCooldown()
        {
            var (userId, bossZoneId, _, _) = await SeedBossDataAsync();

            await using var socketClient = new TestSocketClient();
            var wsClient = Factory.Server.CreateWebSocketClient();
            await socketClient.ConnectAsync(wsClient, userId);

            var response = await socketClient.SendCommandAsync<NewEnemyModel>(
                "ChallengeBoss", new { ZoneId = (int?)bossZoneId });

            Assert.Null(response.Error);
            // No prior battle was abandoned, so nothing anchors the boss's start to a future cooldown.
            Assert.Null(response.Data?.Cooldown);
        }

        [Fact]
        public async Task ChallengeBoss_LoopedWithoutClaimingVictory_PacesTheSecondChallengeWithACooldown()
        {
            var (userId, bossZoneId, _, _) = await SeedBossDataAsync();

            var wsClient = Factory.Server.CreateWebSocketClient();

            await using (var socketClient1 = new TestSocketClient())
            {
                await socketClient1.ConnectAsync(wsClient, userId);

                var first = await socketClient1.SendCommandAsync<NewEnemyModel>(
                    "ChallengeBoss", new { ZoneId = (int?)bossZoneId });
                Assert.Null(first.Error);
                Assert.NotNull(first.Data?.EnemyInstance);

                await socketClient1.CloseAsync();
            }

            // Backdate the in-flight boss battle so the next ChallengeBoss's abandon resolves a real
            // outcome instead of handing it back still in progress (#1595) — mirrors
            // NewEnemy_AbandonResolvesOutcome_BundlesTheIncurredCooldownWithTheReplacementEnemy.
            await SetPlayerState(userId, state =>
            {
                state.BattleStartTime = DateTime.UtcNow.AddMinutes(-30);
            });

            await using var socketClient = new TestSocketClient();
            await socketClient.ConnectAsync(wsClient, userId);

            // Regression coverage for #1884 (the boss-path variant of #1851/#1881): a scripted client that
            // loops ChallengeBoss instead of ever sending DefeatEnemy must not be able to farm away the
            // post-battle pacing cooldown, and the client must be told about it on the very same response
            // so it doesn't present the boss as live ahead of the server's anchor.
            var second = await socketClient.SendCommandAsync<NewEnemyModel>(
                "ChallengeBoss", new { ZoneId = (int?)bossZoneId });

            Assert.Null(second.Error);
            Assert.NotNull(second.Data);
            Assert.NotNull(second.Data.EnemyInstance);
            Assert.NotNull(second.Data.Cooldown);
            Assert.True(second.Data.Cooldown is > 0 and <= 5000);
        }
    }
}
