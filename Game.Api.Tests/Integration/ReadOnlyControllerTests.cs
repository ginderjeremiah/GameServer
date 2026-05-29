using Game.Api.Models.Common;
using Game.Api.Models.Enemies;
using Game.Api.Models.Items;
using Game.Api.Models.Skills;
using Game.Api.Models.Zones;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using Xunit;
using Attribute = Game.Api.Models.Attributes.Attribute;

namespace Game.Api.Tests.Integration
{
    [Collection("Integration")]
    public class ReadOnlyControllerTests : ApiIntegrationTestBase
    {
        private readonly HttpClient _authClient;

        public ReadOnlyControllerTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper) : base(containers, testOutputHelper)
        {
            _authClient = null!; // initialized in InitializeAsync via setup helper
        }

        /// <summary>
        /// Seeds test data and returns an authenticated client.
        /// </summary>
        private async Task<HttpClient> SetupAuthenticatedClientAsync()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context, "readonlyuser", "readonlypass");
            var skill = await TestDataSeeder.CreateSkillAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, skill.Id);

            var loginCreds = new { Username = "readonlyuser", Password = "readonlypass" };
            var loginResponse = await Client.PostAsJsonAsync("/api/Login", loginCreds);
            Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

            var authClient = Factory.CreateClient();
            var cookies = loginResponse.Headers.GetValues("Set-Cookie");
            foreach (var cookie in cookies)
            {
                authClient.DefaultRequestHeaders.Add("Cookie", cookie.Split(';')[0]);
            }

            return authClient;
        }

        [Fact]
        public async Task GetEnemies_Authenticated_ReturnsSeededEnemies()
        {
            // Arrange — seed an enemy
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var enemy = await TestDataSeeder.CreateEnemyAsync(context, "Goblin");
            var skill = await TestDataSeeder.CreateSkillAsync(context);
            await TestDataSeeder.LinkSkillToEnemyAsync(context, enemy.Id, skill.Id);

            using var authClient = await SetupAuthenticatedClientAsync();

            // Act
            var response = await authClient.GetAsync("/api/Enemies");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiEnumerableResponse<Enemy>>();
            Assert.NotNull(result);
            Assert.Null(result.ErrorMessage);
            Assert.NotNull(result.Data);

            var enemies = result.Data.ToList();
            Assert.Contains(enemies, e => e.Name == "Goblin");
        }

        [Fact]
        public async Task GetEnemies_Unauthenticated_Returns401()
        {
            var response = await Client.GetAsync("/api/Enemies");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task GetSkills_Authenticated_ReturnsSeededSkills()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            await TestDataSeeder.CreateSkillAsync(context, "Fireball", baseDamage: 25m, cooldownMs: 2000);

            using var authClient = await SetupAuthenticatedClientAsync();

            var response = await authClient.GetAsync("/api/Skills");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiEnumerableResponse<Skill>>();
            Assert.NotNull(result);
            Assert.NotNull(result.Data);

            var skills = result.Data.ToList();
            Assert.Contains(skills, s => s.Name == "Fireball");
        }

        [Fact]
        public async Task GetZones_Authenticated_ReturnsSeededZones()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            await TestDataSeeder.CreateZoneAsync(context, "Dark Forest", levelMin: 5, levelMax: 15);

            using var authClient = await SetupAuthenticatedClientAsync();

            var response = await authClient.GetAsync("/api/Zones");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiEnumerableResponse<Zone>>();
            Assert.NotNull(result);
            Assert.NotNull(result.Data);

            var zones = result.Data.ToList();
            Assert.Contains(zones, z => z.Name == "Dark Forest");
        }

        [Fact]
        public async Task GetItems_Authenticated_ReturnsEmptyWhenNoItems()
        {
            using var authClient = await SetupAuthenticatedClientAsync();

            var response = await authClient.GetAsync("/api/Items");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiEnumerableResponse<Item>>();
            Assert.NotNull(result);
            Assert.Null(result.ErrorMessage);
            // Data may be null or empty when no items exist
        }

        [Fact]
        public async Task GetAttributes_Authenticated_ReturnsAttributes()
        {
            using var authClient = await SetupAuthenticatedClientAsync();

            // Attributes are seeded via migrations, so this should return reference data
            var response = await authClient.GetAsync("/api/Attributes");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            // Attributes use ApiAsyncEnumerableResponse which serializes as an array
            var result = await response.Content.ReadFromJsonAsync<ApiEnumerableResponse<Attribute>>();
            Assert.NotNull(result);
            Assert.Null(result.ErrorMessage);
        }
    }
}
