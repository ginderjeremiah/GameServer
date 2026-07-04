using Game.Api.Models.Progress;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net.WebSockets;
using Xunit;

namespace Game.Api.Tests.Integration
{
    /// <summary>
    /// Integration tests for the player-scoped <c>GetOfflineProgress</c> socket command (#1042): it computes
    /// and applies the connected player's offline progress and returns the welcome-back summary. Exercises the
    /// command end-to-end through the socket, including the summary DTO projection.
    /// </summary>
    [Collection("Integration")]
    public class GetOfflineProgressSocketTests : ApiIntegrationTestBase
    {
        public GetOfflineProgressSocketTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper) : base(containers, testOutputHelper) { }

        private async Task<TestSocketClient> ConnectAsync(int userId)
        {
            var socketClient = new TestSocketClient();
            var wsClient = Factory.Server.CreateWebSocketClient();
            await socketClient.ConnectAsync(wsClient, userId);
            Assert.Equal(WebSocketState.Open, socketClient.State);
            return socketClient;
        }

        // Seeds a user + player who reliably one-shots a fixed-power enemy in a single idle zone, optionally
        // backdating LastActivity so the player counts as "away" by the requested amount.
        private async Task<int> SeedWinningPlayerAsync(string username, string password, TimeSpan awayFor)
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            // The skills are close enough in output that neither combatant's CombatRating dwarfs the other's —
            // a curve match, not just a raw power match — so the bounty curve (spike #1526 Decision 4) pays a
            // real reward instead of collapsing it toward zero for a wildly one-sided fight.
            var playerSkill = await TestDataSeeder.CreateSkillAsync(context, "Smash", baseDamage: 50m, cooldownMs: 500);
            var enemy = await TestDataSeeder.CreateEnemyAsync(context,
                strengthBase: 50m, strengthPerLevel: 0m, enduranceBase: 50m, endurancePerLevel: 0m);
            var enemySkill = await TestDataSeeder.CreateSkillAsync(context, "Poke", baseDamage: 25m, cooldownMs: 500);
            await TestDataSeeder.LinkSkillToEnemyAsync(context, enemy.Id, enemySkill.Id);
            var zone = await TestDataSeeder.CreateZoneAsync(context, levelMin: 1, levelMax: 1);
            await TestDataSeeder.LinkEnemyToZoneAsync(context, zone.Id, enemy.Id);

            var user = await TestDataSeeder.CreateUserAsync(context, username, password);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id, zoneId: zone.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, playerSkill.Id);

            // Backdate the away-time anchor so the login-time offline check sees a real away window.
            await context.Players
                .Where(p => p.Id == player.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.LastActivity, DateTime.UtcNow - awayFor));

            // The caches no longer lazily refill, so reload them so the session handshake resolves the player.
            await ReloadReferenceCachesAsync();

            return user.Id;
        }

        [Fact]
        public async Task GetOfflineProgress_AfterLongAbsence_ReturnsWelcomeBackSummary()
        {
            var userId = await SeedWinningPlayerAsync("offlineuser", "offlinepass", awayFor: TimeSpan.FromMinutes(30));
            await LoginAsync("offlineuser", "offlinepass");

            await using var socketClient = await ConnectAsync(userId);

            var response = await socketClient.SendCommandAsync<OfflineProgressModel>("GetOfflineProgress");

            Assert.Null(response.Error);
            Assert.NotNull(response.Data);
            Assert.True(response.Data.HasProgress);
            Assert.True(response.Data.BattlesWon > 0);
            Assert.True(response.Data.TotalExp > 0);
            Assert.False(response.Data.AutoChallengeBoss);
        }

        [Fact]
        public async Task GetOfflineProgress_BelowThreshold_ReturnsEmptySummaryWithoutError()
        {
            var userId = await SeedWinningPlayerAsync("offlinenoopuser", "offlinenooppass", awayFor: TimeSpan.FromMinutes(1));
            await LoginAsync("offlinenoopuser", "offlinenooppass");

            await using var socketClient = await ConnectAsync(userId);

            var response = await socketClient.SendCommandAsync<OfflineProgressModel>("GetOfflineProgress");

            Assert.Null(response.Error);
            Assert.NotNull(response.Data);
            // A sub-threshold absence earns nothing — the frontend gate is skipped.
            Assert.False(response.Data.HasProgress);
            Assert.Equal(0, response.Data.BattlesWon);
            Assert.Equal(0, response.Data.TotalExp);
        }
    }
}
