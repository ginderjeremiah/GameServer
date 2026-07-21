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

            // Reload the caches so battle setup resolves the seeded enemy/zone (the caches no longer lazily refill).
            await ReloadReferenceCachesAsync();

            var loginResponse = await Client.PostAsJsonAsync("/api/Auth",
                new { Username = "defeatuser", Password = "defeatpass" });
            Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

            return (user.Id, player.Id);
        }

        private async Task SetPlayerState(int userId, int playerId, Action<PlayerState> modifyState)
        {
            using var scope = CreateScope();
            var sessionService = scope.ServiceProvider.GetRequiredService<SessionService>();
            sessionService.SetAuthenticatedUser(userId);
            await sessionService.LoadPlayerState();
            modifyState(sessionService.PlayerState);
            await sessionService.SavePlayerStateAsync();
        }

        [Fact]
        public async Task DefeatEnemy_EnoughTimeElapsed_ReturnsRewards()
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
                // The battle-start payload carries the enemy's combat rating (spike #1526 Decision 7) alongside
                // its attributes/skills, always strictly positive per CombatRating's degenerate-guard floor.
                Assert.True(newEnemyResponse.Data?.EnemyInstance?.EnemyRating > 0);

                await socketClient1.CloseAsync();
            }

            // Update start time and reconnet socket to ensure DefeatEnemy uses the modified PlayerState
            await SetPlayerState(userId, playerId, state =>
            {
                state.SetActiveBattle(
                    state.ActiveEnemyId!.Value,
                    state.ActiveEnemyLevel!.Value,
                    state.ActiveEnemySkillIds!,
                    state.BattleSeed!.Value,
                    startTime: DateTime.UtcNow.AddMinutes(-30), // pretend battle started 30 minutes ago
                    state.Snapshot!,
                    zoneId: state.BattleZoneId ?? 0,
                    isBossBattle: state.IsBossBattle
                );
            });

            await using var socketClient = new TestSocketClient();
            await socketClient.ConnectAsync(wsClient, userId);
            Assert.Equal(WebSocketState.Open, socketClient.State);

            // The battle was backdated 30 minutes, so more than its duration of server time has elapsed and the
            // victory resolves. The command carries no client timestamp — the server validates off its own clock.
            var response = await socketClient.SendCommandAsync<DefeatEnemyResponse>(
                "DefeatEnemy", new { });

            Assert.Null(response.Error);
            Assert.NotNull(response.Data);
            Assert.NotNull(response.Data.Rewards);
            Assert.True(response.Data.Rewards.ExpReward >= 0);
            // The victory response carries the player's post-battle combat rating (spike #1526 Decision 7) so
            // the client's displayed power number can update immediately on a level-up.
            Assert.True(response.Data.Rewards.PlayerRating > 0);
            // An idle victory bundles the next idle battle so the client begins it without a separate
            // NewEnemy round-trip — its fetch latency is hidden under the post-battle cooldown (#1092).
            Assert.NotNull(response.Data.NextEnemy);
            Assert.NotNull(response.Data.NextZoneId);
        }

        [Fact]
        public async Task DefeatEnemy_BattleCompletedLongBeforePostBattleCooldownWindow_CooldownClampedToZero()
        {
            var (userId, playerId) = await SeedAndLoginAsync();

            var wsClient = Factory.Server.CreateWebSocketClient();

            await using (var socketClient1 = new TestSocketClient())
            {
                await socketClient1.ConnectAsync(wsClient, userId);

                var newEnemyResponse = await socketClient1.SendCommandAsync<NewEnemyModel>(
                    "NewEnemy", new { NewZoneId = (int?)null });
                Assert.Null(newEnemyResponse.Error);

                await socketClient1.CloseAsync();
            }

            // Backdate the battle start well past its own replay duration plus the 5s post-battle cooldown, so
            // the victory's cooldown anchor (battleCompletedAt + cooldown, #2242) is already in the past by the
            // time the command computes the response — reproducing the unclamped-negative-Cooldown scenario.
            await SetPlayerState(userId, playerId, state =>
            {
                state.SetActiveBattle(
                    state.ActiveEnemyId!.Value,
                    state.ActiveEnemyLevel!.Value,
                    state.ActiveEnemySkillIds!,
                    state.BattleSeed!.Value,
                    startTime: DateTime.UtcNow.AddMinutes(-30),
                    state.Snapshot!,
                    zoneId: state.BattleZoneId ?? 0,
                    isBossBattle: state.IsBossBattle
                );
            });

            await using var socketClient = new TestSocketClient();
            await socketClient.ConnectAsync(wsClient, userId);

            var response = await socketClient.SendCommandAsync<DefeatEnemyResponse>(
                "DefeatEnemy", new { });

            Assert.Null(response.Error);
            Assert.NotNull(response.Data);
            Assert.Equal(0, response.Data.Cooldown);
        }

        [Fact]
        public async Task DefeatEnemy_NoActiveBattle_ReturnsError()
        {
            var (userId, _) = await SeedAndLoginAsync();

            await using var socketClient = new TestSocketClient();
            var wsClient = Factory.Server.CreateWebSocketClient();
            await socketClient.ConnectAsync(wsClient, userId);

            var response = await socketClient.SendCommandAsync<DefeatEnemyResponse>(
                "DefeatEnemy", new { });

            Assert.NotNull(response.Error);
        }

        [Fact]
        public async Task DefeatEnemy_ClaimedBeforeBattleCouldFinish_ReturnsError()
        {
            var (userId, _) = await SeedAndLoginAsync();

            await using var socketClient = new TestSocketClient();
            var wsClient = Factory.Server.CreateWebSocketClient();
            await socketClient.ConnectAsync(wsClient, userId);

            // Start battle
            var newEnemyResponse = await socketClient.SendCommandAsync<NewEnemyModel>(
                "NewEnemy", new { NewZoneId = (int?)null });
            Assert.Null(newEnemyResponse.Error);

            // Try to defeat immediately: the battle just started, so far less server time has elapsed than its
            // duration and the claim could not have finished yet — rejected.
            var response = await socketClient.SendCommandAsync<DefeatEnemyResponse>(
                "DefeatEnemy", new { });

            Assert.NotNull(response.Error);
        }
    }
}
