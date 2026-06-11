using Game.Api.Models.Enemies;
using Game.Api.Models.Progress;
using Game.Api.Services;
using Game.Api.Sockets.Commands;
using Game.Core;
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
    /// <summary>
    /// End-to-end coverage for the challenge-completion push: defeating an enemy that completes a
    /// reward-bearing challenge emits a <see cref="ChallengeCompleted"/> command to the player's live
    /// socket, carrying the unlocked reward ids — the server side of the fix that lets a newly-unlocked
    /// item be equipped immediately rather than only after a refresh.
    /// </summary>
    [Collection("Integration")]
    public class ChallengeCompletedSocketTests : ApiIntegrationTestBase
    {
        public ChallengeCompletedSocketTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

        [Fact]
        public async Task DefeatEnemy_CompletesItemRewardChallenge_PushesChallengeCompleted()
        {
            var scenario = await SeedChallengeScenarioAsync();

            var wsClient = Factory.Server.CreateWebSocketClient();

            // Start a battle so PlayerState carries the active-enemy snapshot, then drop that socket.
            await using (var setupSocket = new TestSocketClient())
            {
                await setupSocket.ConnectAsync(wsClient, scenario.UserId);
                var newEnemyResponse = await setupSocket.SendCommandAsync<NewEnemyModel>(
                    "NewEnemy", new { NewZoneId = (int?)null });
                Assert.Null(newEnemyResponse.Error);
                await setupSocket.CloseAsync();
            }

            // Backdate the battle start so the claimed victory is well past the enemy's defeat time.
            await SetPlayerState(scenario.UserId, state =>
            {
                state.SetActiveBattle(
                    state.ActiveEnemyId!.Value,
                    state.ActiveEnemyLevel!.Value,
                    state.ActiveEnemySkillIds!,
                    state.BattleSeed!.Value,
                    startTime: DateTime.UtcNow.AddMinutes(-30),
                    state.Snapshot!,
                    zoneId: state.BattleZoneId ?? 0,
                    isBossBattle: state.IsBossBattle);
            });

            await using var socketClient = new TestSocketClient();
            await socketClient.ConnectAsync(wsClient, scenario.UserId);
            Assert.Equal(WebSocketState.Open, socketClient.State);

            // Send DefeatEnemy without consuming its reply, then wait for the server-pushed completion
            // (no request Id) by name so it can't race the DefeatEnemy response.
            var futureTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            await socketClient.SendCommandNoWaitAsync("DefeatEnemy", new { Timestamp = futureTimestamp });

            var push = await socketClient.WaitForCommandAsync<ChallengeCompletedModel>(nameof(ChallengeCompleted));

            Assert.Null(push.Error);
            Assert.Equal(scenario.ChallengeId, push.Data.ChallengeId);
            Assert.Equal(scenario.ItemId, push.Data.RewardItemId);
            Assert.Null(push.Data.RewardItemModId);
            Assert.Null(push.Data.RewardSkillId);
        }

        /// <summary>
        /// Seeds a player ready to win a single battle that completes an <see cref="EChallengeType.EnemiesKilled"/>
        /// challenge (goal 1) carrying an item reward, then logs in. Reloads the reference caches so the
        /// handler resolves the seeded challenge and reward item.
        /// </summary>
        private async Task<Scenario> SeedChallengeScenarioAsync()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var skill = await TestDataSeeder.CreateSkillAsync(context);
            var enemy = await TestDataSeeder.CreateEnemyAsync(context);
            await TestDataSeeder.LinkSkillToEnemyAsync(context, enemy.Id, skill.Id);
            var zone = await TestDataSeeder.CreateZoneAsync(context);
            await TestDataSeeder.LinkEnemyToZoneAsync(context, zone.Id, enemy.Id);

            var user = await TestDataSeeder.CreateUserAsync(context, "challengeuser", "challengepass");
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id, zoneId: zone.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, skill.Id);

            var item = await TestDataSeeder.CreateItemAsync(context);
            var challenge = await TestDataSeeder.CreateChallengeAsync(
                context,
                challengeTypeId: EChallengeType.EnemiesKilled,
                progressGoal: 1m,
                rewardItemId: item.Id);

            await ReloadReferenceCachesAsync();

            var loginResponse = await Client.PostAsJsonAsync("/api/Login",
                new { Username = "challengeuser", Password = "challengepass" });
            Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

            return new Scenario(user.Id, player.Id, challenge.Id, item.Id);
        }

        private async Task SetPlayerState(int userId, Action<PlayerState> modifyState)
        {
            using var scope = CreateScope();
            var sessionService = scope.ServiceProvider.GetRequiredService<SessionService>();
            await sessionService.LoadPlayerState(userId);
            modifyState(sessionService.PlayerState);
            sessionService.SavePlayerState();
        }

        private sealed record Scenario(int UserId, int PlayerId, int ChallengeId, int ItemId);
    }
}
