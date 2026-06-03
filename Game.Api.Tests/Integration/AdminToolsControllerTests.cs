using Game.Api.Models.Common;
using Game.Api.Models.Enemies;
using Game.Api.Models.Items;
using Game.Api.Models.Progress;
using Game.Api.Models.Skills;
using Game.Api.Models.Zones;
using Game.Core;
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
                        IsBoss= true,
                        AttributeDistribution = Array.Empty<object>(),
                        SkillPool = Array.Empty<int>(),
                        Spawns = Array.Empty<object>()
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
        public async Task AddEditEnemies_AddBossEnemy_PersistsIsBoss()
        {
            using var authClient = await SetupAuthenticatedClientAsync();

            var changes = new[]
            {
                new
                {
                    Item = new
                    {
                        Id = 0,
                        Name = "Ancient Wyrm",
                        IsBoss = true,
                        AttributeDistribution = Array.Empty<object>(),
                        SkillPool = Array.Empty<int>(),
                        Spawns = Array.Empty<object>()
                    },
                    ChangeType = 0 // Add
                }
            };

            var response = await authClient.PostAsJsonAsync("/api/AdminTools/AddEditEnemies", changes, CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var enemiesResponse = await authClient.GetAsync("/api/Enemies", CancellationToken);
            var enemiesResult = await enemiesResponse.Content.ReadFromJsonAsync<ApiEnumerableResponse<Enemy>>(CancellationToken);
            Assert.NotNull(enemiesResult?.Data);
            var boss = Assert.Single(enemiesResult.Data, e => e.Name == "Ancient Wyrm");
            Assert.True(boss.IsBoss);
        }

        [Fact]
        public async Task SetEnemySpawns_ValidEnemy_PersistsSpawn()
        {
            // Arrange — seed an enemy and a zone with no link between them yet.
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var enemy = await TestDataSeeder.CreateEnemyAsync(context, "Spawner");
            var zone = await TestDataSeeder.CreateZoneAsync(context, "Spawn Zone");

            using var authClient = await SetupAuthenticatedClientAsync();

            var data = new
            {
                EnemyId = enemy.Id,
                Spawns = new[]
                {
                    new { ZoneId = zone.Id, Weight = 42 }
                }
            };

            // Act
            var response = await authClient.PostAsJsonAsync("/api/AdminTools/SetEnemySpawns", data, CancellationToken);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.NotNull(result);
            Assert.Null(result.ErrorMessage);

            // The enemy's embedded spawns now include the zone with the assigned weight.
            var enemiesResponse = await authClient.GetAsync("/api/Enemies", CancellationToken);
            var enemiesResult = await enemiesResponse.Content.ReadFromJsonAsync<ApiEnumerableResponse<Enemy>>(CancellationToken);
            Assert.NotNull(enemiesResult?.Data);
            var saved = Assert.Single(enemiesResult.Data, e => e.Id == enemy.Id);
            var spawn = Assert.Single(saved.Spawns);
            Assert.Equal(zone.Id, spawn.ZoneId);
            Assert.Equal(42, spawn.Weight);
        }

        [Fact]
        public async Task SetEnemySpawns_UnknownEnemy_ReturnsError()
        {
            using var authClient = await SetupAuthenticatedClientAsync();

            var data = new
            {
                EnemyId = 999999,
                Spawns = Array.Empty<object>()
            };

            var response = await authClient.PostAsJsonAsync("/api/AdminTools/SetEnemySpawns", data, CancellationToken);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.NotNull(result);
            Assert.Equal("Enemy not found.", result.ErrorMessage);
        }

        [Fact]
        public async Task SetSkillMultipliers_EditExistingMultiplier_UpdatesInPlaceWithoutDuplicatingSkill()
        {
            // Arrange — the first skill after a clean truncate has the zero-based Id 0,
            // which is exactly the case that used to be misread as a new record.
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var skill = await TestDataSeeder.CreateSkillAsync(context, "Fireball"); // Id 0, Strength x1.0

            using var authClient = await SetupAuthenticatedClientAsync();
            var before = await GetSkillsAsync(authClient);

            var data = new
            {
                Id = skill.Id,
                Changes = new[]
                {
                    new
                    {
                        Item = new { AttributeId = (int)EAttribute.Strength, Amount = 2.5m },
                        ChangeType = 1 // Edit
                    }
                }
            };

            // Act
            var response = await authClient.PostAsJsonAsync("/api/AdminTools/SetSkillMultipliers", data, CancellationToken);

            // Assert — the edit succeeds, no skill is duplicated, and the multiplier is updated.
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.NotNull(result);
            Assert.Null(result.ErrorMessage);

            var after = await GetSkillsAsync(authClient);
            Assert.Equal(before.Count, after.Count);
            var edited = Assert.Single(after, s => s.Id == skill.Id);
            var multiplier = Assert.Single(edited.DamageMultipliers);
            Assert.Equal(EAttribute.Strength, multiplier.AttributeId);
            Assert.Equal(2.5m, multiplier.Multiplier);
        }

        [Fact]
        public async Task AddEditSkills_EditZeroIdSkill_UpdatesInPlaceWithoutCreatingDuplicate()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var skill = await TestDataSeeder.CreateSkillAsync(context, "Original"); // Id 0

            using var authClient = await SetupAuthenticatedClientAsync();
            var before = await GetSkillsAsync(authClient);

            var changes = new[]
            {
                new
                {
                    Item = new
                    {
                        Id = skill.Id, // 0 — the identity-column seed / CLR default
                        Name = "Renamed",
                        BaseDamage = 99m,
                        CooldownMs = 1500,
                        Description = "Updated",
                        IconPath = "skills/new.png",
                        DamageMultipliers = Array.Empty<object>()
                    },
                    ChangeType = 1 // Edit
                }
            };

            var response = await authClient.PostAsJsonAsync("/api/AdminTools/AddEditSkills", changes, CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.NotNull(result);
            Assert.Null(result.ErrorMessage);

            var after = await GetSkillsAsync(authClient);
            Assert.Equal(before.Count, after.Count); // editing record 0 must not insert a new skill
            var edited = Assert.Single(after, s => s.Id == skill.Id);
            Assert.Equal("Renamed", edited.Name);
            Assert.Equal(99m, edited.BaseDamage);
        }

        [Fact]
        public async Task AddEditChallenges_AddChallenge_Succeeds()
        {
            using var authClient = await SetupAuthenticatedClientAsync();

            var changes = new[]
            {
                new
                {
                    Item = new
                    {
                        Id = 0,
                        Name = "First Blood",
                        Description = "Defeat your very first foes in battle.",
                        ChallengeTypeId = (int)EChallengeType.EnemiesKilled,
                        TargetEntityId = (int?)null,
                        ProgressGoal = 10m,
                        RewardItemId = (int?)null,
                        RewardItemModId = (int?)null
                    },
                    ChangeType = 0 // Add
                }
            };

            var response = await authClient.PostAsJsonAsync("/api/AdminTools/AddEditChallenges", changes, CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.NotNull(result);
            Assert.Null(result.ErrorMessage);

            // Verify the challenge was created — and its statistic/entity were derived from the type.
            var challengesResponse = await authClient.GetAsync("/api/Challenges", CancellationToken);
            Assert.Equal(HttpStatusCode.OK, challengesResponse.StatusCode);
            var challengesResult = await challengesResponse.Content.ReadFromJsonAsync<ApiEnumerableResponse<Challenge>>(CancellationToken);
            Assert.NotNull(challengesResult?.Data);
            var created = Assert.Single(challengesResult.Data, c => c.Name == "First Blood");
            Assert.Equal(EChallengeType.EnemiesKilled, created.ChallengeTypeId);
            Assert.Equal(EStatisticType.EnemiesKilled, created.StatisticType);
            Assert.Equal(EEntityType.Enemy, created.EntityType);
            Assert.Equal(10m, created.ProgressGoal);
        }

        private async Task<List<Skill>> GetSkillsAsync(HttpClient client)
        {
            var response = await client.GetAsync("/api/Skills", CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiEnumerableResponse<Skill>>(CancellationToken);
            Assert.NotNull(result?.Data);
            return result.Data.ToList();
        }

        [Fact]
        public async Task AddEditItemAttributes_AddEditDelete_RoundTripsThroughTheDatabase()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var item = await TestDataSeeder.CreateItemAsync(context, "Sword"); // Strength = 5

            using var authClient = await SetupAuthenticatedClientAsync();

            // Edit the seeded Strength attribute and add a new Endurance attribute in one batch.
            var addEdit = new
            {
                Id = item.Id,
                Changes = new[]
                {
                    new { Item = new { AttributeId = (int)EAttribute.Strength, Amount = 12.5m }, ChangeType = 1 }, // Edit
                    new { Item = new { AttributeId = (int)EAttribute.Endurance, Amount = 7m }, ChangeType = 0 } // Add
                }
            };

            var response = await authClient.PostAsJsonAsync("/api/AdminTools/AddEditItemAttributes", addEdit, CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.NotNull(result);
            Assert.Null(result.ErrorMessage);

            var saved = Assert.Single(await GetItemsAsync(authClient), i => i.Id == item.Id);
            Assert.Equal(2, saved.Attributes.Count());
            Assert.Equal(12.5m, saved.Attributes.Single(a => a.AttributeId == EAttribute.Strength).Amount);
            Assert.Equal(7m, saved.Attributes.Single(a => a.AttributeId == EAttribute.Endurance).Amount);

            // Now delete the Endurance attribute and confirm only the edited Strength remains.
            var delete = new
            {
                Id = item.Id,
                Changes = new[]
                {
                    new { Item = new { AttributeId = (int)EAttribute.Endurance, Amount = 7m }, ChangeType = 2 } // Delete
                }
            };

            var deleteResponse = await authClient.PostAsJsonAsync("/api/AdminTools/AddEditItemAttributes", delete, CancellationToken);
            Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

            var afterDelete = Assert.Single(await GetItemsAsync(authClient), i => i.Id == item.Id);
            var remaining = Assert.Single(afterDelete.Attributes);
            Assert.Equal(EAttribute.Strength, remaining.AttributeId);
            Assert.Equal(12.5m, remaining.Amount);
        }

        [Fact]
        public async Task AddEditItemModAttributes_AddEditDelete_RoundTripsThroughTheDatabase()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var itemMod = await TestDataSeeder.CreateItemModAsync(context, "Sharp"); // Strength = 5

            using var authClient = await SetupAuthenticatedClientAsync();

            var addEdit = new
            {
                Id = itemMod.Id,
                Changes = new[]
                {
                    new { Item = new { AttributeId = (int)EAttribute.Strength, Amount = 9m }, ChangeType = 1 }, // Edit
                    new { Item = new { AttributeId = (int)EAttribute.Agility, Amount = 3m }, ChangeType = 0 } // Add
                }
            };

            var response = await authClient.PostAsJsonAsync("/api/AdminTools/AddEditItemModAttributes", addEdit, CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.NotNull(result);
            Assert.Null(result.ErrorMessage);

            var saved = Assert.Single(await GetItemModsAsync(authClient), m => m.Id == itemMod.Id);
            Assert.Equal(2, saved.Attributes.Count());
            Assert.Equal(9m, saved.Attributes.Single(a => a.AttributeId == EAttribute.Strength).Amount);
            Assert.Equal(3m, saved.Attributes.Single(a => a.AttributeId == EAttribute.Agility).Amount);

            var delete = new
            {
                Id = itemMod.Id,
                Changes = new[]
                {
                    new { Item = new { AttributeId = (int)EAttribute.Agility, Amount = 3m }, ChangeType = 2 } // Delete
                }
            };

            var deleteResponse = await authClient.PostAsJsonAsync("/api/AdminTools/AddEditItemModAttributes", delete, CancellationToken);
            Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

            var afterDelete = Assert.Single(await GetItemModsAsync(authClient), m => m.Id == itemMod.Id);
            var remaining = Assert.Single(afterDelete.Attributes);
            Assert.Equal(EAttribute.Strength, remaining.AttributeId);
            Assert.Equal(9m, remaining.Amount);
        }

        private async Task<List<Item>> GetItemsAsync(HttpClient client)
        {
            var response = await client.GetAsync("/api/Items", CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiEnumerableResponse<Item>>(CancellationToken);
            Assert.NotNull(result?.Data);
            return result.Data.ToList();
        }

        private async Task<List<ItemMod>> GetItemModsAsync(HttpClient client)
        {
            var response = await client.GetAsync("/api/ItemMods", CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiEnumerableResponse<ItemMod>>(CancellationToken);
            Assert.NotNull(result?.Data);
            return result.Data.ToList();
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
