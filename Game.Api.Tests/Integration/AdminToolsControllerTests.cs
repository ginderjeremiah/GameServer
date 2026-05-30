using Game.Api.Models.Common;
using Game.Api.Models.Enemies;
using Game.Api.Models.Zones;
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
    public class AdminToolsControllerTests : ApiIntegrationTestBase
    {
        public AdminToolsControllerTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper) : base(containers, testOutputHelper) { }

        private async Task<HttpClient> SetupAuthenticatedClientAsync()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context, "adminuser", "adminpass");
            var skill = await TestDataSeeder.CreateSkillAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, skill.Id);

            return CreateAuthenticatedClient(user.Id, player.Id);
        }

        [Fact]
        public async Task AddEditEnemies_AddEnemy_Succeeds()
        {
            using var authClient = await SetupAuthenticatedClientAsync();

            var changes = new[]
            {
                new
                {
                    Item = new
                    {
                        Id = 0,
                        Name = "New Dragon",
                        AttributeDistribution = Array.Empty<object>(),
                        SkillPool = Array.Empty<int>()
                    },
                    ChangeType = 0 // Add
                }
            };

            var response = await authClient.PostAsJsonAsync("/api/AdminTools/AddEditEnemies", changes, CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.NotNull(result);
            Assert.Null(result.ErrorMessage);

            // Verify the enemy was created by fetching enemies
            var enemiesResponse = await authClient.GetAsync("/api/Enemies", CancellationToken);
            Assert.Equal(HttpStatusCode.OK, enemiesResponse.StatusCode);
            var enemiesResult = await enemiesResponse.Content.ReadFromJsonAsync<ApiEnumerableResponse<Enemy>>(CancellationToken);
            Assert.NotNull(enemiesResult?.Data);
            Assert.Contains(enemiesResult.Data, e => e.Name == "New Dragon");
        }

        [Fact]
        public async Task AddEditZones_AddZone_Succeeds()
        {
            using var authClient = await SetupAuthenticatedClientAsync();

            var changes = new[]
            {
                new
                {
                    Item = new
                    {
                        Id = 0,
                        Name = "Crystal Caves",
                        Description = "A glittering underground network",
                        Order = 5,
                        LevelMin = 10,
                        LevelMax = 20
                    },
                    ChangeType = 0 // Add
                }
            };

            var response = await authClient.PostAsJsonAsync("/api/AdminTools/AddEditZones", changes, CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.NotNull(result);
            Assert.Null(result.ErrorMessage);

            // Verify
            var zonesResponse = await authClient.GetAsync("/api/Zones", CancellationToken);
            var zonesResult = await zonesResponse.Content.ReadFromJsonAsync<ApiEnumerableResponse<Zone>>(CancellationToken);
            Assert.NotNull(zonesResult?.Data);
            Assert.Contains(zonesResult.Data, z => z.Name == "Crystal Caves");
        }

        [Fact]
        public async Task SetEnemySkills_ValidEnemy_Succeeds()
        {
            // Arrange — seed enemy and skills
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var enemy = await TestDataSeeder.CreateEnemyAsync(context);
            var skill1 = await TestDataSeeder.CreateSkillAsync(context, "Slash");
            var skill2 = await TestDataSeeder.CreateSkillAsync(context, "Bite");

            using var authClient = await SetupAuthenticatedClientAsync();

            var data = new
            {
                EnemyId = enemy.Id,
                SkillIds = new[] { skill1.Id, skill2.Id }
            };

            // Act
            var response = await authClient.PostAsJsonAsync("/api/AdminTools/SetEnemySkills", data, CancellationToken);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.NotNull(result);
            Assert.Null(result.ErrorMessage);
        }

        [Fact]
        public async Task SetZoneEnemies_ValidZone_Succeeds()
        {
            // Arrange
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var zone = await TestDataSeeder.CreateZoneAsync(context);
            var enemy = await TestDataSeeder.CreateEnemyAsync(context);
            var skill = await TestDataSeeder.CreateSkillAsync(context);
            await TestDataSeeder.LinkSkillToEnemyAsync(context, enemy.Id, skill.Id);

            using var authClient = await SetupAuthenticatedClientAsync();

            var data = new
            {
                ZoneId = zone.Id,
                ZoneEnemies = new[]
                {
                    new { EnemyId = enemy.Id, Weight = 100 }
                }
            };

            // Act
            var response = await authClient.PostAsJsonAsync("/api/AdminTools/SetZoneEnemies", data, CancellationToken);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.NotNull(result);
            Assert.Null(result.ErrorMessage);
        }

        [Fact]
        public async Task AdminTools_Unauthenticated_Returns401()
        {
            var changes = Array.Empty<object>();
            var response = await Client.PostAsJsonAsync("/api/AdminTools/AddEditEnemies", changes, CancellationToken);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }
}
