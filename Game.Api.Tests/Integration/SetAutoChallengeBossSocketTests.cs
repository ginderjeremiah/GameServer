using Game.Infrastructure.Database;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Game.Api.Tests.Integration
{
    [Collection("Integration")]
    public class SetAutoChallengeBossSocketTests : ApiIntegrationTestBase
    {
        private const string Username = "autobossuser";
        private const string Password = "autobosspass";

        public SetAutoChallengeBossSocketTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper) : base(containers, testOutputHelper) { }

        // Seeds a user + player standing in a zone. The zone carries a dedicated boss only when requested,
        // so the same helper drives both the success path (boss zone) and the rejection path (bossless zone).
        private async Task<int> SeedPlayerInZoneAsync(bool withBoss)
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            int? bossEnemyId = null;
            if (withBoss)
            {
                var boss = await TestDataSeeder.CreateEnemyAsync(context, "Zone Boss", isBoss: true);
                bossEnemyId = boss.Id;
            }
            var zone = await TestDataSeeder.CreateZoneAsync(context, "Zone", bossEnemyId: bossEnemyId, bossLevel: 5);

            var skill = await TestDataSeeder.CreateSkillAsync(context);
            var user = await TestDataSeeder.CreateUserAsync(context, Username, Password);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id, zoneId: zone.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, skill.Id);

            // The caches no longer lazily refill, so reload them so the session handshake resolves the player.
            await ReloadReferenceCachesAsync();

            return user.Id;
        }

        [Fact]
        public async Task SetAutoChallengeBoss_EnableInBossZone_Succeeds()
        {
            var userId = await SeedPlayerInZoneAsync(withBoss: true);
            await LoginAsync(Username, Password);

            await using var socketClient = new TestSocketClient();
            var wsClient = Factory.Server.CreateWebSocketClient();
            await socketClient.ConnectAsync(wsClient, userId);

            var response = await socketClient.SendCommandRawAsync("SetAutoChallengeBoss", true);

            Assert.Null(response.Error);
        }

        [Fact]
        public async Task SetAutoChallengeBoss_Disable_Succeeds()
        {
            var userId = await SeedPlayerInZoneAsync(withBoss: true);
            await LoginAsync(Username, Password);

            await using var socketClient = new TestSocketClient();
            var wsClient = Factory.Server.CreateWebSocketClient();
            await socketClient.ConnectAsync(wsClient, userId);

            // Disabling always succeeds — returning to idle needs no boss in the current zone.
            var response = await socketClient.SendCommandRawAsync("SetAutoChallengeBoss", false);

            Assert.Null(response.Error);
        }

        [Fact]
        public async Task SetAutoChallengeBoss_EnableInBosslessZone_ReturnsError()
        {
            var userId = await SeedPlayerInZoneAsync(withBoss: false);
            await LoginAsync(Username, Password);

            await using var socketClient = new TestSocketClient();
            var wsClient = Factory.Server.CreateWebSocketClient();
            await socketClient.ConnectAsync(wsClient, userId);

            // The current zone has no boss, so enabling boss mode is rejected (anti-cheat) rather than applied.
            var response = await socketClient.SendCommandRawAsync("SetAutoChallengeBoss", true);

            Assert.NotNull(response.Error);
        }
    }
}
