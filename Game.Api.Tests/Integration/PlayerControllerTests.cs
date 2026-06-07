using Game.Abstractions.Contracts;
using Game.Api.Models.Attributes;
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

        /// <summary>
        /// Creates a user, player, and skill, then logs in and returns an authenticated client with a valid session.
        /// </summary>
        private async Task<(HttpClient Client, int PlayerId)> CreateAuthenticatedPlayerAsync(
            string username = "playeruser",
            string password = "playerpass",
            int statPointsGained = 100)
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var user = await TestDataSeeder.CreateUserAsync(context, username, password);
            var skill = await TestDataSeeder.CreateSkillAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, skill.Id);

            if (statPointsGained != 100)
            {
                player.StatPointsGained = statPointsGained;
                await context.SaveChangesAsync();
            }

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

        [Fact]
        public async Task UpdatePlayerStats_ValidUpdate_ReturnsUpdatedAttributes()
        {
            // Give extra stat points so we can allocate
            var (authClient, _) = await CreateAuthenticatedPlayerAsync(
                username: "statsuser", password: "statspass", statPointsGained: 106);
            using var client = authClient;

            var updates = new List<AttributeUpdate>
            {
                new() { AttributeId = (int)Game.Core.EAttribute.Strength, Amount = 3 },
            };

            var response = await client.PostAsJsonAsync("/api/Player/UpdatePlayerStats", updates, CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiEnumerableResponse<BattlerAttribute>>(CancellationToken);
            Assert.NotNull(result);
            Assert.Null(result.ErrorMessage);
            Assert.NotNull(result.Data);

            var attributes = result.Data.ToList();
            Assert.NotEmpty(attributes);
        }

        [Fact]
        public async Task UpdatePlayerStats_SpendMoreThanAvailable_ReturnsError()
        {
            var (authClient, _) = await CreateAuthenticatedPlayerAsync(
                username: "overspend", password: "overspend");
            using var client = authClient;

            var updates = new List<AttributeUpdate>
            {
                new() { AttributeId = (int)Game.Core.EAttribute.Strength, Amount = 999 },
            };

            var response = await client.PostAsJsonAsync("/api/Player/UpdatePlayerStats", updates, CancellationToken);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiEnumerableResponse<BattlerAttribute>>(CancellationToken);
            Assert.NotNull(result);
            Assert.NotNull(result.ErrorMessage);
        }
    }
}
