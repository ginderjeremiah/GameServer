using Game.Api.Models.Common;
using Game.Api.Models.Player;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Game.Api.Tests.Integration
{
    [Collection("Integration")]
    public class PlayerControllerTests : ApiIntegrationTestBase
    {
        public PlayerControllerTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper) : base(containers, testOutputHelper) { }

        // Only the player read (GET /api/Player) remains on the HTTP controller; the player-write actions
        // (Equip/Unequip/ApplyMod/RemoveMod) moved to socket commands so they serialize with the battle
        // loop (#463) — they are covered by InventorySocketTests.

        /// <summary>
        /// Creates a user, player, and skill, then logs in and returns an authenticated client with a valid session.
        /// </summary>
        private async Task<(HttpClient Client, int PlayerId)> CreateAuthenticatedPlayerAsync(
            string username = "playeruser",
            string password = "playerpass")
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var user = await TestDataSeeder.CreateUserAsync(context, username, password);
            var skill = await TestDataSeeder.CreateSkillAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, skill.Id);
            // The caches no longer lazily refill, so reload them to resolve the player's linked skill on load.
            await ReloadReferenceCachesAsync();

            // Login to create session and obtain a bearer access token
            var (authClient, _) = await LoginAndBuildClientAsync(username, password);
            return (authClient, player.Id);
        }

        [Fact]
        public async Task GetPlayer_Authenticated_ReturnsPlayerData()
        {
            var (authClient, _) = await CreateAuthenticatedPlayerAsync();
            using var client = authClient;

            var response = await client.GetAsync("/api/Player", CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<PlayerData>>(CancellationToken);
            Assert.NotNull(result);
            Assert.Null(result.ErrorMessage);
            Assert.NotNull(result.Data);
            Assert.Equal("TestPlayer", result.Data.Name);
        }

        [Fact]
        public async Task GetPlayer_Unauthenticated_Returns401()
        {
            var response = await Client.GetAsync("/api/Player", CancellationToken);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }
}
