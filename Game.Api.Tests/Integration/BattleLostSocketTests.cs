using Game.Abstractions.DataAccess;
using Game.Api.Models.Enemies;
using Game.Api.Services;
using Game.Core.Battle;
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
    public class BattleLostSocketTests : ApiIntegrationTestBase
    {
        public BattleLostSocketTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper) : base(containers, testOutputHelper) { }

        private async Task SetPlayerState(int userId, Action<PlayerState> modifyState)
        {
            using var scope = CreateScope();
            var sessionService = scope.ServiceProvider.GetRequiredService<SessionService>();
            sessionService.SetAuthenticatedUser(userId);
            await sessionService.LoadPlayerState();
            modifyState(sessionService.PlayerState);
            await sessionService.SavePlayerStateAsync();
        }

        private async Task<PlayerState> GetPlayerState(int userId)
        {
            using var scope = CreateScope();
            var sessionService = scope.ServiceProvider.GetRequiredService<SessionService>();
            sessionService.SetAuthenticatedUser(userId);
            await sessionService.LoadPlayerState();
            return sessionService.PlayerState;
        }

        private async Task<(int userId, int playerId)> SeedWeakPlayerVsStrongEnemyAsync()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            // Weak player skill
            var skill = await TestDataSeeder.CreateSkillAsync(context, "Poke", baseDamage: 1m, cooldownMs: 2000);

            // Strong enemy with strong skill
            var strongEnemy = await TestDataSeeder.CreateStrongEnemyAsync(context);
            var enemySkill = await TestDataSeeder.CreateSkillAsync(context, "Crush", baseDamage: 100m, cooldownMs: 500);
            await TestDataSeeder.LinkSkillToEnemyAsync(context, strongEnemy.Id, enemySkill.Id);
            var zone = await TestDataSeeder.CreateZoneAsync(context);
            await TestDataSeeder.LinkEnemyToZoneAsync(context, zone.Id, strongEnemy.Id);

            var user = await TestDataSeeder.CreateUserAsync(context, "weakplayer", "weakpass");
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id, zoneId: zone.Id, level: 1);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, skill.Id);

            // Weaken the player's stats
            var existingAttrs = context.PlayerAttributes.Where(pa => pa.PlayerId == player.Id);
            context.PlayerAttributes.RemoveRange(existingAttrs);
            context.PlayerAttributes.AddRange(
                new Game.Infrastructure.Entities.PlayerAttribute
                {
                    PlayerId = player.Id,
                    AttributeId = (int)Game.Core.EAttribute.Strength,
                    Amount = 1m
                },
                new Game.Infrastructure.Entities.PlayerAttribute
                {
                    PlayerId = player.Id,
                    AttributeId = (int)Game.Core.EAttribute.Endurance,
                    Amount = 1m
                });
            player.StatPointsGained = 2;
            player.StatPointsUsed = 2;
            await context.SaveChangesAsync();

            // Reload the caches so battle setup resolves the seeded enemy/zone (the caches no longer lazily refill).
            await ReloadReferenceCachesAsync();

            var loginResponse = await Client.PostAsJsonAsync("/api/Auth",
                new { Username = "weakplayer", Password = "weakpass" });
            Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

            return (user.Id, player.Id);
        }

        private async Task<int> SeedNormalPlayerAsync()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var skill = await TestDataSeeder.CreateSkillAsync(context);
            var enemy = await TestDataSeeder.CreateEnemyAsync(context);
            await TestDataSeeder.LinkSkillToEnemyAsync(context, enemy.Id, skill.Id);
            var zone = await TestDataSeeder.CreateZoneAsync(context);
            await TestDataSeeder.LinkEnemyToZoneAsync(context, zone.Id, enemy.Id);

            var user = await TestDataSeeder.CreateUserAsync(context, "lossuser", "losspass");
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id, zoneId: zone.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, skill.Id);

            // Reload the caches so battle setup resolves the seeded enemy/zone (the caches no longer lazily refill).
            await ReloadReferenceCachesAsync();

            var loginResponse = await Client.PostAsJsonAsync("/api/Auth",
                new { Username = "lossuser", Password = "losspass" });
            Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

            return user.Id;
        }

        [Fact]
        public async Task BattleLost_NoActiveBattle_ReturnsError()
        {
            var userId = await SeedNormalPlayerAsync();

            await using var socketClient = new TestSocketClient();
            var wsClient = Factory.Server.CreateWebSocketClient();
            await socketClient.ConnectAsync(wsClient, userId);
            Assert.Equal(WebSocketState.Open, socketClient.State);

            var response = await socketClient.SendCommandAsync<BattleLostResponse>("BattleLost", new { });

            Assert.NotNull(response.Error);
        }

        [Fact]
        public async Task BattleLost_ValidLoss_Succeeds()
        {
            var (userId, _) = await SeedWeakPlayerVsStrongEnemyAsync();

            var wsClient = Factory.Server.CreateWebSocketClient();

            await using (var socketClient1 = new TestSocketClient())
            {
                await socketClient1.ConnectAsync(wsClient, userId);
                Assert.Equal(WebSocketState.Open, socketClient1.State);

                // Start battle against strong enemy
                var newEnemyResponse = await socketClient1.SendCommandAsync<NewEnemyModel>(
                    "NewEnemy", new { NewZoneId = (int?)null });
                Assert.Null(newEnemyResponse.Error);
                Assert.NotNull(newEnemyResponse.Data?.EnemyInstance);

                await socketClient1.CloseAsync();
            }

            // Backdate the battle start so more than the simulated loss's replay duration has elapsed,
            // satisfying the server-measured elapsed-time check (#1630).
            await SetPlayerState(userId, state =>
            {
                state.BattleStartTime = DateTime.UtcNow.AddMinutes(-30);
            });

            await using var socketClient = new TestSocketClient();
            await socketClient.ConnectAsync(wsClient, userId);

            // Report loss, including the client-simulated duration (diagnostic only — the server validates
            // off its own clock, exercised at the BattleService level in EndBattleLoss_DivergentClientTotalMs_
            // IsDiagnosticOnly_StillReturnsTrue).
            var response = await socketClient.SendCommandAsync<BattleLostResponse>(
                "BattleLost", new { ClientTotalMs = 1234 });

            Assert.Null(response.Error);
            Assert.NotNull(response.Data);
            Assert.True(response.Data.Cooldown >= 0);
            // A boss loss returns to the idle farm, so the next idle battle is bundled with the loss response
            // — letting the client begin it without a separate NewEnemy round-trip after the cooldown (#1092).
            Assert.NotNull(response.Data.NextEnemy);
            Assert.NotNull(response.Data.NextZoneId);
        }

        [Fact]
        public async Task BattleLost_ClaimedBeforeBattleCouldFinish_ReturnsError()
        {
            var (userId, _) = await SeedWeakPlayerVsStrongEnemyAsync();

            await using var socketClient = new TestSocketClient();
            var wsClient = Factory.Server.CreateWebSocketClient();
            await socketClient.ConnectAsync(wsClient, userId);

            // Start battle against strong enemy
            var newEnemyResponse = await socketClient.SendCommandAsync<NewEnemyModel>(
                "NewEnemy", new { NewZoneId = (int?)null });
            Assert.Null(newEnemyResponse.Error);

            // Report the loss immediately: the battle just started, so far less server time has elapsed than
            // its replay duration and the claim could not have finished yet — rejected (#1630).
            var response = await socketClient.SendCommandAsync<BattleLostResponse>("BattleLost", new { });

            Assert.NotNull(response.Error);
        }

        [Fact]
        public async Task BattleLost_BattleAlreadyCredited_PersistsClearedBattleState()
        {
            var (userId, playerId) = await SeedWeakPlayerVsStrongEnemyAsync();

            var wsClient = Factory.Server.CreateWebSocketClient();

            await using (var socketClient1 = new TestSocketClient())
            {
                await socketClient1.ConnectAsync(wsClient, userId);

                var newEnemyResponse = await socketClient1.SendCommandAsync<NewEnemyModel>(
                    "NewEnemy", new { NewZoneId = (int?)null });
                Assert.Null(newEnemyResponse.Error);

                await socketClient1.CloseAsync();
            }

            // Capture this battle's identity before crediting it, then backdate so it resolves as a genuine
            // loss — the fields are restored onto the session further down to reproduce the reconnect gap
            // (#1874/#1993) this test targets.
            int activeEnemyId = 0, activeEnemyLevel = 0, battleZoneId = 0;
            List<int> activeEnemySkillIds = null!;
            uint battleSeed = 0;
            BattleSnapshot snapshot = null!;
            bool isBossBattle = false;

            await SetPlayerState(userId, state =>
            {
                activeEnemyId = state.ActiveEnemyId!.Value;
                activeEnemyLevel = state.ActiveEnemyLevel!.Value;
                activeEnemySkillIds = state.ActiveEnemySkillIds!;
                battleSeed = state.BattleSeed!.Value;
                snapshot = state.Snapshot!;
                battleZoneId = state.BattleZoneId ?? 0;
                isBossBattle = state.IsBossBattle;

                state.SetActiveBattle(
                    activeEnemyId, activeEnemyLevel, activeEnemySkillIds, battleSeed,
                    startTime: DateTime.UtcNow.AddMinutes(-30),
                    snapshot, zoneId: battleZoneId, isBossBattle: isBossBattle);
            });

            await using (var socketClient2 = new TestSocketClient())
            {
                await socketClient2.ConnectAsync(wsClient, userId);

                var creditResponse = await socketClient2.SendCommandAsync<BattleLostResponse>("BattleLost", new { });
                Assert.Null(creditResponse.Error);

                await socketClient2.CloseAsync();
            }

            // The player-cache write behind the credit is fire-and-forget (docs/backend-persistence.md), so
            // poll until it lands before restoring the session to the now-credited battle below.
            using (var scope = CreateScope())
            {
                var playerRepo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
                await PollingHelper.PollUntilAsync(
                    () => playerRepo.GetPlayer(playerId),
                    p => p?.LastCreditedBattleSeed == battleSeed);
            }

            // Reproduce the reconnect gap (#1874/#1993): the durable credit landed, but the session's own
            // PlayerState still shows this exact (now-credited) battle as active.
            await SetPlayerState(userId, state =>
            {
                state.SetActiveBattle(
                    activeEnemyId, activeEnemyLevel, activeEnemySkillIds, battleSeed,
                    startTime: DateTime.UtcNow.AddMinutes(-30),
                    snapshot, zoneId: battleZoneId, isBossBattle: isBossBattle);
            });

            await using (var socketClient3 = new TestSocketClient())
            {
                await socketClient3.ConnectAsync(wsClient, userId);

                var rejectedResponse = await socketClient3.SendCommandAsync<BattleLostResponse>("BattleLost", new { });
                Assert.NotNull(rejectedResponse.Error);

                await socketClient3.CloseAsync();
            }

            // The rejection clears the stale battle in memory (BattleAlreadyCredited) — pin that the clear is
            // actually persisted rather than lost the moment socketClient3 disconnects (#2345).
            var persistedState = await GetPlayerState(userId);
            Assert.False(persistedState.HasActiveBattle);
        }
    }
}
