using Game.Abstractions.Contracts;
using Game.Abstractions.DataAccess;
using Game.Api.Models.Common;
using Game.Core;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using Xunit;
using CoreChallenge = Game.Core.Progress.Challenge;

namespace Game.Api.Tests.Integration
{
    [Collection("Integration")]
    public class AdminToolsControllerTests : ApiIntegrationTestBase
    {
        public AdminToolsControllerTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper) : base(containers, testOutputHelper) { }

        private async Task<HttpClient> SetupAuthenticatedClientAsync(bool admin = true)
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context, "adminuser", "adminpass");
            var skill = await TestDataSeeder.CreateSkillAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, skill.Id);

            var roles = admin ? new[] { nameof(ERole.Admin) } : [];
            return CreateAuthenticatedClient(user.Id, player.Id, roles);
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

            // Verify the enemy was created — the admin write invalidates the cache, so a fresh read sees it.
            Assert.Contains(GetEnemies(), e => e.Name == "New Dragon");
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
            Assert.Contains(GetZones(), z => z.Name == "Crystal Caves");
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

            var boss = Assert.Single(GetEnemies(), e => e.Name == "Ancient Wyrm");
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
            var saved = Assert.Single(GetEnemies(), e => e.Id == enemy.Id);
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
            var before = GetSkills();

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

            var after = GetSkills();
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
            var before = GetSkills();

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

            var after = GetSkills();
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
            var created = Assert.Single(GetChallenges(), c => c.Name == "First Blood");
            Assert.Equal(EChallengeType.EnemiesKilled, created.Type.Id);
            Assert.Equal(EStatisticType.EnemiesKilled, created.Type.StatisticType?.Id);
            Assert.Equal(EEntityType.Enemy, created.Type.StatisticType?.EntityType);
            Assert.Equal(10m, created.ProgressGoal);
        }

        // The reference-data HTTP GET endpoints were removed (#64); reads now go over the socket, which
        // serves the same in-memory caches these repositories expose. The admin write filter invalidates
        // those caches, so reading through the repository after a write returns the freshly-persisted data
        // — the same guarantee the Workbench relies on — while keeping these assertions transport-agnostic.
        private List<Skill> GetSkills()
        {
            using var scope = CreateScope();
            return scope.ServiceProvider.GetRequiredService<ISkills>().AllSkills().ToList();
        }

        private List<Enemy> GetEnemies()
        {
            using var scope = CreateScope();
            return scope.ServiceProvider.GetRequiredService<IEnemies>().All().ToList();
        }

        private List<Zone> GetZones()
        {
            using var scope = CreateScope();
            return scope.ServiceProvider.GetRequiredService<IZones>().All().ToList();
        }

        private List<CoreChallenge> GetChallenges()
        {
            using var scope = CreateScope();
            return scope.ServiceProvider.GetRequiredService<IChallenges>().All().ToList();
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

            var saved = Assert.Single(GetItems(), i => i.Id == item.Id);
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

            var afterDelete = Assert.Single(GetItems(), i => i.Id == item.Id);
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

            var saved = Assert.Single(GetItemMods(), m => m.Id == itemMod.Id);
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

            var afterDelete = Assert.Single(GetItemMods(), m => m.Id == itemMod.Id);
            var remaining = Assert.Single(afterDelete.Attributes);
            Assert.Equal(EAttribute.Strength, remaining.AttributeId);
            Assert.Equal(9m, remaining.Amount);
        }

        private List<Item> GetItems()
        {
            using var scope = CreateScope();
            return scope.ServiceProvider.GetRequiredService<IItems>().All().ToList();
        }

        private List<ItemMod> GetItemMods()
        {
            using var scope = CreateScope();
            return scope.ServiceProvider.GetRequiredService<IItemMods>().All().ToList();
        }

        [Fact]
        public async Task AddEditItems_AddItem_Succeeds()
        {
            using var authClient = await SetupAuthenticatedClientAsync();

            var changes = new[]
            {
                new
                {
                    Item = new
                    {
                        Id = 0,
                        Name = "Magic Sword",
                        Description = "A blade humming with power",
                        ItemCategoryId = (int)EItemCategory.Weapon,
                        RarityId = (int)ERarity.Common,
                        IconPath = "items/sword.png",
                        Attributes = Array.Empty<object>(),
                        ModSlots = Array.Empty<object>(),
                        Tags = Array.Empty<int>()
                    },
                    ChangeType = 0 // Add
                }
            };

            var response = await authClient.PostAsJsonAsync("/api/AdminTools/AddEditItems", changes, CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.NotNull(result);
            Assert.Null(result.ErrorMessage);

            Assert.Contains(GetItems(), i => i.Name == "Magic Sword");
        }

        [Fact]
        public async Task AddEditItemMods_AddItemMod_Succeeds()
        {
            using var authClient = await SetupAuthenticatedClientAsync();

            var changes = new[]
            {
                new
                {
                    Item = new
                    {
                        Id = 0,
                        Name = "Flaming",
                        Description = "Adds fire damage",
                        ItemModTypeId = (int)EItemModType.Prefix,
                        RarityId = (int)ERarity.Common,
                        Attributes = Array.Empty<object>(),
                        Tags = Array.Empty<int>()
                    },
                    ChangeType = 0 // Add
                }
            };

            var response = await authClient.PostAsJsonAsync("/api/AdminTools/AddEditItemMods", changes, CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.NotNull(result);
            Assert.Null(result.ErrorMessage);

            Assert.Contains(GetItemMods(), m => m.Name == "Flaming");
        }

        [Fact]
        public async Task AddEditItemModSlots_AddSlot_PersistsSlot()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var item = await TestDataSeeder.CreateItemAsync(context, "Slotted");

            using var authClient = await SetupAuthenticatedClientAsync();

            var changes = new[]
            {
                new
                {
                    Item = new
                    {
                        Id = 0,
                        ItemId = item.Id,
                        ItemModSlotTypeId = (int)EItemModType.Prefix
                    },
                    ChangeType = 0 // Add
                }
            };

            var response = await authClient.PostAsJsonAsync("/api/AdminTools/AddEditItemModSlots", changes, CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.NotNull(result);
            Assert.Null(result.ErrorMessage);

            var saved = Assert.Single(GetItems(), i => i.Id == item.Id);
            var slot = Assert.Single(saved.ModSlots);
            Assert.Equal(EItemModType.Prefix, slot.ItemModSlotTypeId);
        }

        [Fact]
        public async Task AddEditTags_AddTag_Succeeds()
        {
            using var authClient = await SetupAuthenticatedClientAsync();

            var changes = new[]
            {
                new
                {
                    Item = new
                    {
                        Id = 0,
                        Name = "Fire",
                        TagCategoryId = (int)ETagCategory.Accessory
                    },
                    ChangeType = 0 // Add
                }
            };

            var response = await authClient.PostAsJsonAsync("/api/AdminTools/AddEditTags", changes, CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.NotNull(result);
            Assert.Null(result.ErrorMessage);

            Assert.Contains(await GetTags(), t => t.Name == "Fire" && t.TagCategoryId == (int)ETagCategory.Accessory);
        }

        [Fact]
        public async Task SetEnemyAttributeDistributions_ValidEnemy_PersistsDistribution()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            // The seeded enemy starts with Strength and Endurance distributions.
            var enemy = await TestDataSeeder.CreateEnemyAsync(context, "Distributor");

            using var authClient = await SetupAuthenticatedClientAsync();

            // Replace the distributions with a single Strength entry — Endurance should be removed.
            var data = new
            {
                EnemyId = enemy.Id,
                AttributeDistributions = new[]
                {
                    new { AttributeId = (int)EAttribute.Strength, BaseAmount = 20m, AmountPerLevel = 3m }
                }
            };

            var response = await authClient.PostAsJsonAsync("/api/AdminTools/SetEnemyAttributeDistributions", data, CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.NotNull(result);
            Assert.Null(result.ErrorMessage);

            var saved = Assert.Single(GetEnemies(), e => e.Id == enemy.Id);
            var distribution = Assert.Single(saved.AttributeDistribution);
            Assert.Equal(EAttribute.Strength, distribution.AttributeId);
            Assert.Equal(20m, distribution.BaseAmount);
            Assert.Equal(3m, distribution.AmountPerLevel);
        }

        [Fact]
        public async Task SetEnemyAttributeDistributions_UnknownEnemy_ReturnsError()
        {
            using var authClient = await SetupAuthenticatedClientAsync();

            var data = new
            {
                EnemyId = 999999,
                AttributeDistributions = Array.Empty<object>()
            };

            var response = await authClient.PostAsJsonAsync("/api/AdminTools/SetEnemyAttributeDistributions", data, CancellationToken);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.NotNull(result);
            Assert.Equal("Enemy not found.", result.ErrorMessage);
        }

        [Fact]
        public async Task SetTagsForItem_ValidItem_AssociatesTags()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var item = await TestDataSeeder.CreateItemAsync(context, "Tagged Item");
            var tag1 = await TestDataSeeder.CreateTagAsync(context, "Sharp");
            var tag2 = await TestDataSeeder.CreateTagAsync(context, "Heavy");

            using var authClient = await SetupAuthenticatedClientAsync();

            var data = new
            {
                Id = item.Id,
                TagIds = new[] { tag1.Id, tag2.Id }
            };

            var response = await authClient.PostAsJsonAsync("/api/AdminTools/SetTagsForItem", data, CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.NotNull(result);
            Assert.Null(result.ErrorMessage);

            var tags = await GetTagsForItem(item.Id);
            Assert.Equal(2, tags.Count);
            Assert.Contains(tags, t => t.Id == tag1.Id);
            Assert.Contains(tags, t => t.Id == tag2.Id);
        }

        [Fact]
        public async Task SetTagsForItem_UnknownItem_ReturnsError()
        {
            using var authClient = await SetupAuthenticatedClientAsync();

            var data = new
            {
                Id = 999999,
                TagIds = Array.Empty<int>()
            };

            var response = await authClient.PostAsJsonAsync("/api/AdminTools/SetTagsForItem", data, CancellationToken);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.NotNull(result);
            Assert.Equal("Item not found.", result.ErrorMessage);
        }

        [Fact]
        public async Task SetTagsForItemMod_ValidItemMod_AssociatesTags()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var itemMod = await TestDataSeeder.CreateItemModAsync(context, "Tagged Mod");
            var tag = await TestDataSeeder.CreateTagAsync(context, "Fiery");

            using var authClient = await SetupAuthenticatedClientAsync();

            var data = new
            {
                Id = itemMod.Id,
                TagIds = new[] { tag.Id }
            };

            var response = await authClient.PostAsJsonAsync("/api/AdminTools/SetTagsForItemMod", data, CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.NotNull(result);
            Assert.Null(result.ErrorMessage);

            var tags = await GetTagsForItemMod(itemMod.Id);
            var associated = Assert.Single(tags);
            Assert.Equal(tag.Id, associated.Id);
        }

        private async Task<List<Tag>> GetTagsForItem(int itemId)
        {
            using var scope = CreateScope();
            return await scope.ServiceProvider.GetRequiredService<ITags>().GetTagsForItem(itemId).ToListAsync();
        }

        private async Task<List<Tag>> GetTagsForItemMod(int itemModId)
        {
            using var scope = CreateScope();
            return await scope.ServiceProvider.GetRequiredService<ITags>().GetTagsForItemMod(itemModId).ToListAsync();
        }

        private async Task<List<Tag>> GetTags()
        {
            using var scope = CreateScope();
            return await scope.ServiceProvider.GetRequiredService<ITags>().All().ToListAsync();
        }

        [Fact]
        public async Task AdminTools_Unauthenticated_Returns401()
        {
            var changes = Array.Empty<object>();
            var response = await Client.PostAsJsonAsync("/api/AdminTools/AddEditEnemies", changes, CancellationToken);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task AdminTools_AuthenticatedWithoutAdminRole_Returns403()
        {
            // An authenticated user that has not been granted the Admin role is forbidden.
            using var authClient = await SetupAuthenticatedClientAsync(admin: false);

            var changes = Array.Empty<object>();
            var response = await authClient.PostAsJsonAsync("/api/AdminTools/AddEditEnemies", changes, CancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }
}
