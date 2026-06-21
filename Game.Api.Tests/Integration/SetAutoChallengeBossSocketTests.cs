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

        private async Task<(int UserId, int BossZoneId)> SeedPlayerWithBossZoneAsync()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var boss = await TestDataSeeder.CreateEnemyAsync(context, "Zone Boss", isBoss: true);
            var bossZone = await TestDataSeeder.CreateZoneAsync(context, "Boss Zone", bossEnemyId: boss.Id, bossLevel: 5);

            var skill = await TestDataSeeder.CreateSkillAsync(context);
            var user = await TestDataSeeder.CreateUserAsync(context, Username, Password);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id, zoneId: bossZone.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, skill.Id);

            // The caches no longer lazily refill, so reload them so the session handshake resolves the player.
            await ReloadReferenceCachesAsync();

            return (user.Id, bossZone.Id);
        }

        [Fact]
        public async Task SetAutoChallengeBoss_EnableValidBossZone_Succeeds()
        {
            var (userId, bossZoneId) = await SeedPlayerWithBossZoneAsync();
            await LoginAsync(Username, Password);

            await using var socketClient = new TestSocketClient();
            var wsClient = Factory.Server.CreateWebSocketClient();
            await socketClient.ConnectAsync(wsClient, userId);

            var response = await socketClient.SendCommandRawAsync(
                "SetAutoChallengeBoss", new { Enabled = true, ZoneId = bossZoneId });

            Assert.Null(response.Error);
        }

        [Fact]
        public async Task SetAutoChallengeBoss_Disable_Succeeds()
        {
            var (userId, _) = await SeedPlayerWithBossZoneAsync();
            await LoginAsync(Username, Password);

            await using var socketClient = new TestSocketClient();
            var wsClient = Factory.Server.CreateWebSocketClient();
            await socketClient.ConnectAsync(wsClient, userId);

            // Disabling needs no valid zone — returning to idle always succeeds (the zone is ignored).
            var response = await socketClient.SendCommandRawAsync(
                "SetAutoChallengeBoss", new { Enabled = false, ZoneId = 0 });

            Assert.Null(response.Error);
        }

        [Fact]
        public async Task SetAutoChallengeBoss_EnableInvalidZone_ReturnsError()
        {
            var (userId, _) = await SeedPlayerWithBossZoneAsync();
            await LoginAsync(Username, Password);

            await using var socketClient = new TestSocketClient();
            var wsClient = Factory.Server.CreateWebSocketClient();
            await socketClient.ConnectAsync(wsClient, userId);

            // An out-of-range zone id is rejected (anti-cheat), surfacing an error rather than mutating state.
            var response = await socketClient.SendCommandRawAsync(
                "SetAutoChallengeBoss", new { Enabled = true, ZoneId = 9999 });

            Assert.NotNull(response.Error);
        }
    }
}
