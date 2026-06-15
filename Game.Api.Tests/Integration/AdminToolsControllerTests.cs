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
            var client = CreateAuthenticatedClient(user.Id, player.Id, roles);

            // Tests seed their reference data directly before calling this, so reload the caches to mirror
            // the production invariant that they are warm before any admin action. The admin write path
            // reads the targeted record from the cache to validate it (an unknown id is a "not found"
            // rejection), and the caches no longer lazily refill.
            await ReloadReferenceCachesAsync();
            return client;
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
        public async Task AddEditEnemies_RetireEnemy_PersistsRetiredAtAndKeepsItResolvable()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var enemy = await TestDataSeeder.CreateEnemyAsync(context, "Doomed");

            using var authClient = await SetupAuthenticatedClientAsync();

            // Retiring is an ordinary Edit that stamps RetiredAt — the admin tools no longer hard-delete
            // top-level reference records.
            var changes = new[]
            {
                new
                {
                    Item = new
                    {
                        enemy.Id,
                        enemy.Name,
                        IsBoss = false,
                        RetiredAt = DateTime.UtcNow,
                        AttributeDistribution = Array.Empty<object>(),
                        SkillPool = Array.Empty<int>(),
                        Spawns = Array.Empty<object>()
                    },
                    ChangeType = 1 // Edit
                }
            };

            var response = await authClient.PostAsJsonAsync("/api/AdminTools/AddEditEnemies", changes, CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var saved = Assert.Single(GetEnemies(), e => e.Id == enemy.Id);
            Assert.NotNull(saved.RetiredAt);
        }

        [Fact]
        public async Task AddEditEnemies_DeleteEnemy_IsRejectedAndLeavesRecordIntact()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var enemy = await TestDataSeeder.CreateEnemyAsync(context, "Persistent");

            using var authClient = await SetupAuthenticatedClientAsync();

            // A top-level Delete against a zero-based-id reference set is unsupported (it would open an
            // id gap that mis-resolves index lookups) — the batch fails and the record is untouched.
            var changes = new[]
            {
                new
                {
                    Item = new
                    {
                        enemy.Id,
                        enemy.Name,
                        IsBoss = false,
                        AttributeDistribution = Array.Empty<object>(),
                        SkillPool = Array.Empty<int>(),
                        Spawns = Array.Empty<object>()
                    },
                    ChangeType = 2 // Delete
                }
            };

            // The change set is rejected before anything is committed (the test host rethrows the
            // action's exception), so the record is left untouched.
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => authClient.PostAsJsonAsync("/api/AdminTools/AddEditEnemies", changes, CancellationToken));

            Assert.Contains(GetEnemies(), e => e.Id == enemy.Id);
        }

        [Fact]
        public async Task AddEditEnemies_EditUnknownEnemy_ReturnsErrorAndPersistsNothing()
        {
            using var authClient = await SetupAuthenticatedClientAsync();

            // An identity-level edit of a non-existent enemy is a not-found rejection (not a 500 from the
            // 0-row update), and the whole batch is rejected up front — so the valid Add alongside it is
            // not persisted.
            var changes = new[]
            {
                new
                {
                    Item = new
                    {
                        Id = 0,
                        Name = "Ghost Enemy",
                        IsBoss = false,
                        AttributeDistribution = Array.Empty<object>(),
                        SkillPool = Array.Empty<int>(),
                        Spawns = Array.Empty<object>()
                    },
                    ChangeType = 0 // Add
                },
                new
                {
                    Item = new
                    {
                        Id = 999999,
                        Name = "Phantom",
                        IsBoss = false,
                        AttributeDistribution = Array.Empty<object>(),
                        SkillPool = Array.Empty<int>(),
                        Spawns = Array.Empty<object>()
                    },
                    ChangeType = 1 // Edit
                }
            };

            var response = await authClient.PostAsJsonAsync("/api/AdminTools/AddEditEnemies", changes, CancellationToken);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.NotNull(result);
            Assert.Equal("Enemy not found.", result.ErrorMessage);
            Assert.DoesNotContain(GetEnemies(), e => e.Name == "Ghost Enemy");
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
        public async Task AddEditZones_WithDedicatedBoss_PersistsBossEnemyAndLevel()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var boss = await TestDataSeeder.CreateEnemyAsync(context, "Catacomb Lich", isBoss: true);

            using var authClient = await SetupAuthenticatedClientAsync();

            var changes = new[]
            {
                new
                {
                    Item = new
                    {
                        Id = 0,
                        Name = "Forgotten Catacombs",
                        Description = "Endless night.",
                        Order = 3,
                        LevelMin = 8,
                        LevelMax = 11,
                        BossEnemyId = (int?)boss.Id,
                        BossLevel = 18
                    },
                    ChangeType = 0 // Add
                }
            };

            var response = await authClient.PostAsJsonAsync("/api/AdminTools/AddEditZones", changes, CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.NotNull(result);
            Assert.Null(result.ErrorMessage);

            var saved = Assert.Single(GetZones(), z => z.Name == "Forgotten Catacombs");
            Assert.Equal(boss.Id, saved.BossEnemyId);
            Assert.Equal(18, saved.BossLevel);
        }

        [Fact]
        public async Task AddEditZones_BossEnemyNotMarkedAsBoss_ReturnsErrorAndPersistsNothing()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var normalEnemy = await TestDataSeeder.CreateEnemyAsync(context, "Plague Rat"); // isBoss: false

            using var authClient = await SetupAuthenticatedClientAsync();

            var changes = new[]
            {
                new
                {
                    Item = new
                    {
                        Id = 0,
                        Name = "Rejected Zone",
                        Description = "x",
                        Order = 0,
                        LevelMin = 1,
                        LevelMax = 5,
                        BossEnemyId = (int?)normalEnemy.Id,
                        BossLevel = 5
                    },
                    ChangeType = 0 // Add
                }
            };

            var response = await authClient.PostAsJsonAsync("/api/AdminTools/AddEditZones", changes, CancellationToken);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.NotNull(result);
            Assert.Contains("boss", result.ErrorMessage ?? "", StringComparison.OrdinalIgnoreCase);

            // The rejected batch must not have been partially applied.
            Assert.DoesNotContain(GetZones(), z => z.Name == "Rejected Zone");
        }

        [Fact]
        public async Task AddEditZones_NonexistentBossEnemy_ReturnsError()
        {
            using var authClient = await SetupAuthenticatedClientAsync();

            var changes = new[]
            {
                new
                {
                    Item = new
                    {
                        Id = 0,
                        Name = "Ghost Zone",
                        Description = "x",
                        Order = 0,
                        LevelMin = 1,
                        LevelMax = 5,
                        BossEnemyId = (int?)999999,
                        BossLevel = 5
                    },
                    ChangeType = 0 // Add
                }
            };

            var response = await authClient.PostAsJsonAsync("/api/AdminTools/AddEditZones", changes, CancellationToken);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.NotNull(result);
            Assert.Contains("boss", result.ErrorMessage ?? "", StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(GetZones(), z => z.Name == "Ghost Zone");
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
        public async Task SetEnemySkills_DropsUnlistedSkill_RemovesIt()
        {
            // Arrange — an enemy already linked to two skills.
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var enemy = await TestDataSeeder.CreateEnemyAsync(context, "Skilled");
            var keptSkill = await TestDataSeeder.CreateSkillAsync(context, "Slash");
            var removedSkill = await TestDataSeeder.CreateSkillAsync(context, "Bite");
            await TestDataSeeder.LinkSkillToEnemyAsync(context, enemy.Id, keptSkill.Id);
            await TestDataSeeder.LinkSkillToEnemyAsync(context, enemy.Id, removedSkill.Id);

            using var authClient = await SetupAuthenticatedClientAsync();

            // Act — narrow the skill pool to a single skill, dropping the other.
            var data = new
            {
                EnemyId = enemy.Id,
                SkillIds = new[] { keptSkill.Id }
            };
            var response = await authClient.PostAsJsonAsync("/api/AdminTools/SetEnemySkills", data, CancellationToken);

            // Assert — only the kept skill survives the delete path.
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var saved = Assert.Single(GetEnemies(), e => e.Id == enemy.Id);
            Assert.Equal(keptSkill.Id, Assert.Single(saved.SkillPool));
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
        public async Task SetZoneEnemies_DropsUnlistedEnemy_RemovesIt()
        {
            // Arrange — a zone with two enemies assigned.
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var zone = await TestDataSeeder.CreateZoneAsync(context, "Crowded Zone");
            var keptEnemy = await TestDataSeeder.CreateEnemyAsync(context, "Resident");
            var removedEnemy = await TestDataSeeder.CreateEnemyAsync(context, "Evicted");
            await TestDataSeeder.LinkEnemyToZoneAsync(context, zone.Id, keptEnemy.Id, 10);
            await TestDataSeeder.LinkEnemyToZoneAsync(context, zone.Id, removedEnemy.Id, 20);

            using var authClient = await SetupAuthenticatedClientAsync();

            // Act — keep only one enemy assigned to the zone.
            var data = new
            {
                ZoneId = zone.Id,
                ZoneEnemies = new[]
                {
                    new { EnemyId = keptEnemy.Id, Weight = 10 }
                }
            };
            var response = await authClient.PostAsJsonAsync("/api/AdminTools/SetZoneEnemies", data, CancellationToken);

            // Assert — only the kept enemy remains linked to the zone.
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var verifyScope = CreateScope();
            var verifyContext = verifyScope.ServiceProvider.GetRequiredService<GameContext>();
            var remaining = await verifyContext.Set<Game.Infrastructure.Entities.ZoneEnemy>()
                .Where(ze => ze.ZoneId == zone.Id)
                .Select(ze => ze.EnemyId)
                .ToListAsync(CancellationToken);
            Assert.Equal(keptEnemy.Id, Assert.Single(remaining));
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
        public async Task SetEnemySpawns_DropsUnlistedSpawn_RemovesIt()
        {
            // Arrange — an enemy spawning in two zones.
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var enemy = await TestDataSeeder.CreateEnemyAsync(context, "Wanderer");
            var keptZone = await TestDataSeeder.CreateZoneAsync(context, "Kept Zone");
            var removedZone = await TestDataSeeder.CreateZoneAsync(context, "Removed Zone");
            await TestDataSeeder.LinkEnemyToZoneAsync(context, keptZone.Id, enemy.Id, 10);
            await TestDataSeeder.LinkEnemyToZoneAsync(context, removedZone.Id, enemy.Id, 20);

            using var authClient = await SetupAuthenticatedClientAsync();

            // Act — keep only one spawn, dropping the other zone.
            var data = new
            {
                EnemyId = enemy.Id,
                Spawns = new[]
                {
                    new { ZoneId = keptZone.Id, Weight = 10 }
                }
            };
            var response = await authClient.PostAsJsonAsync("/api/AdminTools/SetEnemySpawns", data, CancellationToken);

            // Assert — only the kept spawn survives the delete path.
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var saved = Assert.Single(GetEnemies(), e => e.Id == enemy.Id);
            Assert.Equal(keptZone.Id, Assert.Single(saved.Spawns).ZoneId);
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
        public async Task SetSkillEffects_AddEditDelete_RoundTripsThroughTheDatabase()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var skill = await TestDataSeeder.CreateSkillAsync(context, "Poison Sting"); // Id 0

            using var authClient = await SetupAuthenticatedClientAsync();

            // Add two effects: a self Strength buff and an opponent Defense debuff.
            var addData = new
            {
                Id = skill.Id,
                Changes = new[]
                {
                    new
                    {
                        Item = new
                        {
                            Id = 0,
                            Target = (int)ESkillEffectTarget.Self,
                            AttributeId = (int)EAttribute.Strength,
                            ModifierTypeId = (int)EModifierType.Additive,
                            Amount = 15m,
                            DurationMs = 5000
                        },
                        ChangeType = 0 // Add
                    },
                    new
                    {
                        Item = new
                        {
                            Id = 0,
                            Target = (int)ESkillEffectTarget.Opponent,
                            AttributeId = (int)EAttribute.Defense,
                            ModifierTypeId = (int)EModifierType.Multiplicative,
                            Amount = 0.5m,
                            DurationMs = 3000
                        },
                        ChangeType = 0 // Add
                    }
                }
            };

            var addResponse = await authClient.PostAsJsonAsync("/api/AdminTools/SetSkillEffects", addData, CancellationToken);
            Assert.Equal(HttpStatusCode.OK, addResponse.StatusCode);

            var afterAdd = Assert.Single(GetSkills(), s => s.Id == skill.Id);
            Assert.Equal(2, afterAdd.Effects.Count());
            var selfBuff = Assert.Single(afterAdd.Effects, e => e.Target == ESkillEffectTarget.Self);
            Assert.Equal(EAttribute.Strength, selfBuff.AttributeId);
            Assert.Equal(EModifierType.Additive, selfBuff.ModifierTypeId);
            Assert.Equal(15m, selfBuff.Amount);
            Assert.Equal(5000, selfBuff.DurationMs);
            var debuff = Assert.Single(afterAdd.Effects, e => e.Target == ESkillEffectTarget.Opponent);
            Assert.Equal(EAttribute.Defense, debuff.AttributeId);

            // Edit the self buff's amount and delete the opponent debuff.
            var editData = new
            {
                Id = skill.Id,
                Changes = new[]
                {
                    new
                    {
                        Item = new
                        {
                            selfBuff.Id,
                            Target = (int)ESkillEffectTarget.Self,
                            AttributeId = (int)EAttribute.Strength,
                            ModifierTypeId = (int)EModifierType.Additive,
                            Amount = 25m,
                            DurationMs = 5000
                        },
                        ChangeType = 1 // Edit
                    },
                    new
                    {
                        Item = new
                        {
                            debuff.Id,
                            Target = (int)ESkillEffectTarget.Opponent,
                            AttributeId = (int)EAttribute.Defense,
                            ModifierTypeId = (int)EModifierType.Multiplicative,
                            Amount = 0.5m,
                            DurationMs = 3000
                        },
                        ChangeType = 2 // Delete
                    }
                }
            };

            var editResponse = await authClient.PostAsJsonAsync("/api/AdminTools/SetSkillEffects", editData, CancellationToken);
            Assert.Equal(HttpStatusCode.OK, editResponse.StatusCode);

            var afterEdit = Assert.Single(GetSkills(), s => s.Id == skill.Id);
            var remaining = Assert.Single(afterEdit.Effects);
            Assert.Equal(selfBuff.Id, remaining.Id);
            Assert.Equal(ESkillEffectTarget.Self, remaining.Target);
            Assert.Equal(25m, remaining.Amount);

            // The domain projection (SkillMapper.ToCore, which #333's battle runtime consumes)
            // resolves the same effect: decimal→double amount, enum casts, surrogate id.
            var domainSkill = GetDomainSkill(skill.Id);
            var domainEffect = Assert.Single(domainSkill.Effects);
            Assert.Equal(selfBuff.Id, domainEffect.Id);
            Assert.Equal(ESkillEffectTarget.Self, domainEffect.Target);
            Assert.Equal(EAttribute.Strength, domainEffect.AttributeId);
            Assert.Equal(EModifierType.Additive, domainEffect.ModifierType);
            Assert.Equal(25.0, domainEffect.Amount);
            Assert.Equal(5000, domainEffect.DurationMs);
        }

        [Fact]
        public async Task SetSkillEffects_MissingSkill_ReturnsError()
        {
            using var authClient = await SetupAuthenticatedClientAsync();

            var data = new
            {
                Id = 999,
                Changes = new[]
                {
                    new
                    {
                        Item = new
                        {
                            Id = 0,
                            Target = (int)ESkillEffectTarget.Self,
                            AttributeId = (int)EAttribute.Strength,
                            ModifierTypeId = (int)EModifierType.Additive,
                            Amount = 5m,
                            DurationMs = 1000
                        },
                        ChangeType = 0 // Add
                    }
                }
            };

            var response = await authClient.PostAsJsonAsync("/api/AdminTools/SetSkillEffects", data, CancellationToken);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.NotNull(result);
            Assert.Equal("Skill not found.", result.ErrorMessage);
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
                        DamageMultipliers = Array.Empty<object>(),
                        Effects = Array.Empty<object>()
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
        public async Task AddEditSkills_EditUnknownSkill_ReturnsErrorAndPersistsNothing()
        {
            using var authClient = await SetupAuthenticatedClientAsync();

            // An identity-level edit of a non-existent skill is a not-found rejection (not a 500 from the
            // 0-row update), and the whole batch is rejected up front — so the valid Add alongside it is
            // not persisted.
            var changes = new[]
            {
                new
                {
                    Item = new
                    {
                        Id = 0,
                        Name = "Ghost Skill",
                        BaseDamage = 10m,
                        CooldownMs = 1000,
                        Description = "Should never be saved",
                        IconPath = "skills/ghost.png",
                        DamageMultipliers = Array.Empty<object>(),
                        Effects = Array.Empty<object>()
                    },
                    ChangeType = 0 // Add
                },
                new
                {
                    Item = new
                    {
                        Id = 999999,
                        Name = "Phantom",
                        BaseDamage = 1m,
                        CooldownMs = 1000,
                        Description = "x",
                        IconPath = "skills/phantom.png",
                        DamageMultipliers = Array.Empty<object>(),
                        Effects = Array.Empty<object>()
                    },
                    ChangeType = 1 // Edit
                }
            };

            var response = await authClient.PostAsJsonAsync("/api/AdminTools/AddEditSkills", changes, CancellationToken);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.NotNull(result);
            Assert.Equal("Skill not found.", result.ErrorMessage);
            Assert.DoesNotContain(GetSkills(), s => s.Name == "Ghost Skill");
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

        [Fact]
        public async Task AddEditChallenges_AddChallengeWithSkillReward_RoundTripsRewardSkillId()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var skill = await TestDataSeeder.CreateSkillAsync(context, "Fireball"); // Id 0

            using var authClient = await SetupAuthenticatedClientAsync();

            var changes = new[]
            {
                new
                {
                    Item = new
                    {
                        Id = 0,
                        Name = "Pyromancer",
                        Description = "Defeat enough foes to learn Fireball.",
                        ChallengeTypeId = (int)EChallengeType.EnemiesKilled,
                        TargetEntityId = (int?)null,
                        ProgressGoal = 25m,
                        RewardItemId = (int?)null,
                        RewardItemModId = (int?)null,
                        RewardSkillId = (int?)skill.Id
                    },
                    ChangeType = 0 // Add
                }
            };

            var response = await authClient.PostAsJsonAsync("/api/AdminTools/AddEditChallenges", changes, CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.NotNull(result);
            Assert.Null(result.ErrorMessage);

            var created = Assert.Single(GetChallenges(), c => c.Name == "Pyromancer");
            Assert.Equal(skill.Id, created.RewardSkillId);
        }

        [Fact]
        public async Task AddEditChallenges_EditUnknownChallenge_ReturnsErrorAndPersistsNothing()
        {
            using var authClient = await SetupAuthenticatedClientAsync();

            // An identity-level edit of a non-existent challenge is a not-found rejection (not a 500 from
            // the 0-row update), and the whole batch is rejected up front — so the valid Add alongside it
            // is not persisted.
            var changes = new[]
            {
                new
                {
                    Item = new
                    {
                        Id = 0,
                        Name = "Ghost Challenge",
                        Description = "Should never be saved",
                        ChallengeTypeId = (int)EChallengeType.EnemiesKilled,
                        TargetEntityId = (int?)null,
                        ProgressGoal = 5m,
                        RewardItemId = (int?)null,
                        RewardItemModId = (int?)null
                    },
                    ChangeType = 0 // Add
                },
                new
                {
                    Item = new
                    {
                        Id = 999999,
                        Name = "Phantom",
                        Description = "x",
                        ChallengeTypeId = (int)EChallengeType.EnemiesKilled,
                        TargetEntityId = (int?)null,
                        ProgressGoal = 1m,
                        RewardItemId = (int?)null,
                        RewardItemModId = (int?)null
                    },
                    ChangeType = 1 // Edit
                }
            };

            var response = await authClient.PostAsJsonAsync("/api/AdminTools/AddEditChallenges", changes, CancellationToken);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.NotNull(result);
            Assert.Equal("Challenge not found.", result.ErrorMessage);
            Assert.DoesNotContain(GetChallenges(), c => c.Name == "Ghost Challenge");
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

        private Game.Core.Skills.Skill GetDomainSkill(int skillId)
        {
            using var scope = CreateScope();
            return scope.ServiceProvider.GetRequiredService<ISkills>().GetSkill(skillId);
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

        // Tags keep the hard-delete lifecycle (they are .find/Map-keyed, not index-keyed, so they don't suffer
        // the index mis-resolution bug #289 addressed for the six zero-based-id reference sets). This verifies
        // that hard-deleting an *in-use* tag is graceful: the FK_*Tags_Tags cascade removes the join rows rather
        // than throwing, and the referencing item/mod stay resolvable (just without that tag).
        [Fact]
        public async Task AddEditTags_DeleteInUseTag_CascadesJoinRowsAndLeavesReferencesResolvable()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var item = await TestDataSeeder.CreateItemAsync(context, "Tagged Sword");
            var itemMod = await TestDataSeeder.CreateItemModAsync(context, "Tagged Rune");
            var tag = await TestDataSeeder.CreateTagAsync(context, "Doomed");

            // Apply the tag to both an item and a mod so the delete must cascade two join tables.
            var itemEntity = await context.Items.Include(i => i.Tags).FirstAsync(i => i.Id == item.Id, CancellationToken);
            var modEntity = await context.ItemMods.Include(m => m.Tags).FirstAsync(m => m.Id == itemMod.Id, CancellationToken);
            itemEntity.Tags.Add(await context.Tags.FirstAsync(t => t.Id == tag.Id, CancellationToken));
            modEntity.Tags.Add(await context.Tags.FirstAsync(t => t.Id == tag.Id, CancellationToken));
            await context.SaveChangesAsync(CancellationToken);

            using var authClient = await SetupAuthenticatedClientAsync();

            var changes = new[]
            {
                new
                {
                    Item = new { Id = tag.Id, Name = "Doomed", TagCategoryId = (int)ETagCategory.Accessory },
                    ChangeType = 2 // Delete
                }
            };

            var response = await authClient.PostAsJsonAsync("/api/AdminTools/AddEditTags", changes, CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.NotNull(result);
            Assert.Null(result.ErrorMessage);

            // The tag is gone, the join rows cascaded away, and the item/mod remain (sans the deleted tag).
            Assert.DoesNotContain(await GetTags(), t => t.Id == tag.Id);
            Assert.Empty(await GetTagsForItem(item.Id));
            Assert.Empty(await GetTagsForItemMod(itemMod.Id));
            Assert.Contains(GetItems(), i => i.Id == item.Id);
            Assert.Contains(GetItemMods(), m => m.Id == itemMod.Id);
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

        [Fact]
        public async Task SetTagsForItemMod_UnknownItemMod_ReturnsError()
        {
            using var authClient = await SetupAuthenticatedClientAsync();

            var data = new
            {
                Id = 999999,
                TagIds = Array.Empty<int>()
            };

            var response = await authClient.PostAsJsonAsync("/api/AdminTools/SetTagsForItemMod", data, CancellationToken);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.NotNull(result);
            Assert.Equal("Item mod not found.", result.ErrorMessage);
        }

        [Fact]
        public async Task SetTagsForItem_RemovesUnlistedTag_LeavesOtherItemsTagged()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var itemA = await TestDataSeeder.CreateItemAsync(context, "Item A");
            var itemB = await TestDataSeeder.CreateItemAsync(context, "Item B");
            var tag = await TestDataSeeder.CreateTagAsync(context, "Shared");

            using var authClient = await SetupAuthenticatedClientAsync();

            // Both items carry the shared tag.
            await authClient.PostAsJsonAsync("/api/AdminTools/SetTagsForItem",
                new { Id = itemA.Id, TagIds = new[] { tag.Id } }, CancellationToken);
            await authClient.PostAsJsonAsync("/api/AdminTools/SetTagsForItem",
                new { Id = itemB.Id, TagIds = new[] { tag.Id } }, CancellationToken);

            // Re-set item A to drop the tag.
            var response = await authClient.PostAsJsonAsync("/api/AdminTools/SetTagsForItem",
                new { Id = itemA.Id, TagIds = Array.Empty<int>() }, CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            // Item A lost the tag; item B must keep it.
            Assert.Empty(await GetTagsForItem(itemA.Id));
            var bTags = await GetTagsForItem(itemB.Id);
            var remaining = Assert.Single(bTags);
            Assert.Equal(tag.Id, remaining.Id);
        }

        [Fact]
        public async Task SetTagsForItemMod_RemovesUnlistedTag_LeavesOtherModsTagged()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var modA = await TestDataSeeder.CreateItemModAsync(context, "Mod A");
            var modB = await TestDataSeeder.CreateItemModAsync(context, "Mod B");
            var tag = await TestDataSeeder.CreateTagAsync(context, "SharedMod");

            using var authClient = await SetupAuthenticatedClientAsync();

            // Both mods carry the shared tag.
            await authClient.PostAsJsonAsync("/api/AdminTools/SetTagsForItemMod",
                new { Id = modA.Id, TagIds = new[] { tag.Id } }, CancellationToken);
            await authClient.PostAsJsonAsync("/api/AdminTools/SetTagsForItemMod",
                new { Id = modB.Id, TagIds = new[] { tag.Id } }, CancellationToken);

            // Re-set mod A to drop the tag.
            var response = await authClient.PostAsJsonAsync("/api/AdminTools/SetTagsForItemMod",
                new { Id = modA.Id, TagIds = Array.Empty<int>() }, CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            // Mod A lost the tag; mod B must keep it.
            Assert.Empty(await GetTagsForItemMod(modA.Id));
            var bTags = await GetTagsForItemMod(modB.Id);
            var remaining = Assert.Single(bTags);
            Assert.Equal(tag.Id, remaining.Id);
        }

        private async Task<List<Tag>> GetTagsForItem(int itemId)
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            return await context.Tags
                .Where(t => context.ItemTags.Any(it => it.ItemId == itemId && it.TagId == t.Id))
                .Select(t => new Tag { Id = t.Id, Name = t.Name, TagCategoryId = t.TagCategoryId })
                .ToListAsync();
        }

        private async Task<List<Tag>> GetTagsForItemMod(int itemModId)
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            return await context.Tags
                .Where(t => context.ItemModTags.Any(imt => imt.ItemModId == itemModId && imt.TagId == t.Id))
                .Select(t => new Tag { Id = t.Id, Name = t.Name, TagCategoryId = t.TagCategoryId })
                .ToListAsync();
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

        // -------------------------------------------------------------------------------------------------
        // Coverage fill (#407): the admin repositories' edit/retire field-mapping paths and the not-found
        // guards their controllers map to a user-facing error. These pin the easy-to-get-subtly-wrong
        // hand-mapping (a save→read-back round-trip) and the false-returning branches callers depend on.
        // -------------------------------------------------------------------------------------------------

        [Fact]
        public async Task AddEditChallenges_EditRetireReinstate_RoundTripsThroughTheDatabase()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var challenge = await TestDataSeeder.CreateChallengeAsync(context, "Original"); // Id 0, EnemiesKilled, goal 10
            var rewardItem = await TestDataSeeder.CreateItemAsync(context, "Trophy");

            using var authClient = await SetupAuthenticatedClientAsync();
            var before = GetChallenges();

            // Every field is re-mapped on edit; only RetiredAt differs between the three saves below.
            Task<HttpResponseMessage> SaveChallenge(DateTime? retiredAt) =>
                authClient.PostAsJsonAsync("/api/AdminTools/AddEditChallenges", new[]
                {
                    new
                    {
                        Item = new
                        {
                            challenge.Id,
                            Name = "Renamed",
                            Description = "Updated description.",
                            ChallengeTypeId = (int)EChallengeType.BossesDefeated,
                            TargetEntityId = (int?)null,
                            ProgressGoal = 42m,
                            RewardItemId = (int?)rewardItem.Id,
                            RewardItemModId = (int?)null,
                            RewardSkillId = (int?)null,
                            RetiredAt = retiredAt
                        },
                        ChangeType = 1 // Edit
                    }
                }, CancellationToken);

            // Edit in place — editing record 0 must not insert a duplicate, and every field round-trips.
            Assert.Equal(HttpStatusCode.OK, (await SaveChallenge(null)).StatusCode);
            var after = GetChallenges();
            Assert.Equal(before.Count, after.Count);
            var edited = Assert.Single(after, c => c.Id == challenge.Id);
            Assert.Equal("Renamed", edited.Name);
            Assert.Equal("Updated description.", edited.Description);
            Assert.Equal(EChallengeType.BossesDefeated, edited.Type.Id);
            Assert.Equal(42m, edited.ProgressGoal);
            Assert.Equal(rewardItem.Id, edited.RewardItemId);
            Assert.Null(edited.RetiredAt);

            // Retiring is an ordinary edit that stamps RetiredAt; the challenge stays resolvable by id.
            Assert.Equal(HttpStatusCode.OK, (await SaveChallenge(DateTime.UtcNow)).StatusCode);
            Assert.NotNull(Assert.Single(GetChallenges(), c => c.Id == challenge.Id).RetiredAt);

            // Reinstating clears it.
            Assert.Equal(HttpStatusCode.OK, (await SaveChallenge(null)).StatusCode);
            Assert.Null(Assert.Single(GetChallenges(), c => c.Id == challenge.Id).RetiredAt);
        }

        [Fact]
        public async Task AddEditTags_EditTag_UpdatesNameAndCategory()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var tag = await TestDataSeeder.CreateTagAsync(context, "Old", ETagCategory.Accessory);

            using var authClient = await SetupAuthenticatedClientAsync();

            var changes = new[]
            {
                new
                {
                    Item = new { tag.Id, Name = "New", TagCategoryId = (int)ETagCategory.Armor },
                    ChangeType = 1 // Edit
                }
            };

            var response = await authClient.PostAsJsonAsync("/api/AdminTools/AddEditTags", changes, CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var edited = Assert.Single(await GetTags(), t => t.Id == tag.Id);
            Assert.Equal("New", edited.Name);
            Assert.Equal((int)ETagCategory.Armor, edited.TagCategoryId);
        }

        [Fact]
        public async Task AddEditItems_EditRetireReinstate_RoundTripsThroughTheDatabase()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var item = await TestDataSeeder.CreateItemAsync(context, "Original"); // Id 0, Weapon/Common

            using var authClient = await SetupAuthenticatedClientAsync();
            var before = GetItems();

            Task<HttpResponseMessage> SaveItem(DateTime? retiredAt) =>
                authClient.PostAsJsonAsync("/api/AdminTools/AddEditItems", new[]
                {
                    new
                    {
                        Item = new
                        {
                            item.Id,
                            Name = "Renamed",
                            Description = "Updated",
                            ItemCategoryId = (int)EItemCategory.Helm,
                            RarityId = (int)ERarity.Rare,
                            IconPath = "items/new.png",
                            Attributes = Array.Empty<object>(),
                            ModSlots = Array.Empty<object>(),
                            Tags = Array.Empty<int>(),
                            RetiredAt = retiredAt
                        },
                        ChangeType = 1 // Edit
                    }
                }, CancellationToken);

            Assert.Equal(HttpStatusCode.OK, (await SaveItem(null)).StatusCode);
            var after = GetItems();
            Assert.Equal(before.Count, after.Count); // editing record 0 must not insert a new item
            var edited = Assert.Single(after, i => i.Id == item.Id);
            Assert.Equal("Renamed", edited.Name);
            Assert.Equal(EItemCategory.Helm, edited.ItemCategoryId);
            Assert.Equal(ERarity.Rare, edited.RarityId);
            Assert.Null(edited.RetiredAt);

            Assert.Equal(HttpStatusCode.OK, (await SaveItem(DateTime.UtcNow)).StatusCode);
            Assert.NotNull(Assert.Single(GetItems(), i => i.Id == item.Id).RetiredAt);

            Assert.Equal(HttpStatusCode.OK, (await SaveItem(null)).StatusCode);
            Assert.Null(Assert.Single(GetItems(), i => i.Id == item.Id).RetiredAt);
        }

        [Fact]
        public async Task AddEditItems_EditUnknownItem_ReturnsErrorAndPersistsNothing()
        {
            using var authClient = await SetupAuthenticatedClientAsync();

            // An identity-level edit of a non-existent item is a not-found rejection (not a silent no-op),
            // and the whole batch is rejected up front — so the valid Add alongside it is not persisted.
            var changes = new[]
            {
                new
                {
                    Item = new
                    {
                        Id = 0,
                        Name = "Ghost Item",
                        Description = "Should never be saved",
                        ItemCategoryId = (int)EItemCategory.Weapon,
                        RarityId = (int)ERarity.Common,
                        IconPath = "items/ghost.png",
                        Attributes = Array.Empty<object>(),
                        ModSlots = Array.Empty<object>(),
                        Tags = Array.Empty<int>()
                    },
                    ChangeType = 0 // Add
                },
                new
                {
                    Item = new
                    {
                        Id = 999999,
                        Name = "Phantom",
                        Description = "x",
                        ItemCategoryId = (int)EItemCategory.Weapon,
                        RarityId = (int)ERarity.Common,
                        IconPath = "items/phantom.png",
                        Attributes = Array.Empty<object>(),
                        ModSlots = Array.Empty<object>(),
                        Tags = Array.Empty<int>()
                    },
                    ChangeType = 1 // Edit
                }
            };

            var response = await authClient.PostAsJsonAsync("/api/AdminTools/AddEditItems", changes, CancellationToken);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.NotNull(result);
            Assert.Equal("Item not found.", result.ErrorMessage);
            Assert.DoesNotContain(GetItems(), i => i.Name == "Ghost Item");
        }

        [Fact]
        public async Task AddEditItemAttributes_UnknownItem_ReturnsError()
        {
            using var authClient = await SetupAuthenticatedClientAsync();

            var data = new { Id = 999999, Changes = Array.Empty<object>() };

            var response = await authClient.PostAsJsonAsync("/api/AdminTools/AddEditItemAttributes", data, CancellationToken);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.NotNull(result);
            Assert.Equal("Item not found.", result.ErrorMessage);
        }

        [Fact]
        public async Task AddEditItemAttributes_EditOrDeleteAbsentAttribute_LeavesAttributesUnchanged()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var item = await TestDataSeeder.CreateItemAsync(context, "Sword"); // Strength = 5

            using var authClient = await SetupAuthenticatedClientAsync();

            // Editing/deleting attributes the item doesn't have are guarded no-ops (the .Any() match fails),
            // so the batch succeeds without touching the existing Strength attribute.
            var data = new
            {
                Id = item.Id,
                Changes = new[]
                {
                    new { Item = new { AttributeId = (int)EAttribute.Agility, Amount = 9m }, ChangeType = 1 }, // Edit absent
                    new { Item = new { AttributeId = (int)EAttribute.Endurance, Amount = 0m }, ChangeType = 2 } // Delete absent
                }
            };

            var response = await authClient.PostAsJsonAsync("/api/AdminTools/AddEditItemAttributes", data, CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var saved = Assert.Single(GetItems(), i => i.Id == item.Id);
            var attribute = Assert.Single(saved.Attributes);
            Assert.Equal(EAttribute.Strength, attribute.AttributeId);
            Assert.Equal(5m, attribute.Amount);
        }

        [Fact]
        public async Task AddEditItemModSlots_EditAndDelete_RoundTripsThroughTheDatabase()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var item = await TestDataSeeder.CreateItemAsync(context, "Slotted");
            var slot = await TestDataSeeder.AddItemModSlotAsync(context, item.Id, EItemModType.Prefix);

            using var authClient = await SetupAuthenticatedClientAsync();

            // Edit the slot's type in place.
            var edit = new[]
            {
                new
                {
                    Item = new { slot.Id, ItemId = item.Id, ItemModSlotTypeId = (int)EItemModType.Suffix },
                    ChangeType = 1 // Edit
                }
            };
            var editResponse = await authClient.PostAsJsonAsync("/api/AdminTools/AddEditItemModSlots", edit, CancellationToken);
            Assert.Equal(HttpStatusCode.OK, editResponse.StatusCode);
            var afterEdit = Assert.Single(GetItems(), i => i.Id == item.Id);
            Assert.Equal(EItemModType.Suffix, Assert.Single(afterEdit.ModSlots).ItemModSlotTypeId);

            // Delete it.
            var delete = new[]
            {
                new
                {
                    Item = new { slot.Id, ItemId = item.Id, ItemModSlotTypeId = (int)EItemModType.Suffix },
                    ChangeType = 2 // Delete
                }
            };
            var deleteResponse = await authClient.PostAsJsonAsync("/api/AdminTools/AddEditItemModSlots", delete, CancellationToken);
            Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
            Assert.Empty(Assert.Single(GetItems(), i => i.Id == item.Id).ModSlots);
        }

        [Fact]
        public async Task AddEditItemMods_EditRetireReinstate_RoundTripsThroughTheDatabase()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var itemMod = await TestDataSeeder.CreateItemModAsync(context, "Original"); // Id 0, Prefix/Common

            using var authClient = await SetupAuthenticatedClientAsync();
            var before = GetItemMods();

            Task<HttpResponseMessage> SaveItemMod(DateTime? retiredAt) =>
                authClient.PostAsJsonAsync("/api/AdminTools/AddEditItemMods", new[]
                {
                    new
                    {
                        Item = new
                        {
                            itemMod.Id,
                            Name = "Renamed",
                            Description = "Updated",
                            ItemModTypeId = (int)EItemModType.Suffix,
                            RarityId = (int)ERarity.Rare,
                            Attributes = Array.Empty<object>(),
                            Tags = Array.Empty<int>(),
                            RetiredAt = retiredAt
                        },
                        ChangeType = 1 // Edit
                    }
                }, CancellationToken);

            Assert.Equal(HttpStatusCode.OK, (await SaveItemMod(null)).StatusCode);
            var after = GetItemMods();
            Assert.Equal(before.Count, after.Count);
            var edited = Assert.Single(after, m => m.Id == itemMod.Id);
            Assert.Equal("Renamed", edited.Name);
            Assert.Equal(EItemModType.Suffix, edited.ItemModTypeId);
            Assert.Equal(ERarity.Rare, edited.RarityId);
            Assert.Null(edited.RetiredAt);

            Assert.Equal(HttpStatusCode.OK, (await SaveItemMod(DateTime.UtcNow)).StatusCode);
            Assert.NotNull(Assert.Single(GetItemMods(), m => m.Id == itemMod.Id).RetiredAt);

            Assert.Equal(HttpStatusCode.OK, (await SaveItemMod(null)).StatusCode);
            Assert.Null(Assert.Single(GetItemMods(), m => m.Id == itemMod.Id).RetiredAt);
        }

        [Fact]
        public async Task AddEditItemMods_EditUnknownItemMod_ReturnsErrorAndPersistsNothing()
        {
            using var authClient = await SetupAuthenticatedClientAsync();

            // An identity-level edit of a non-existent item mod is a not-found rejection (not a silent no-op),
            // and the whole batch is rejected up front — so the valid Add alongside it is not persisted.
            var changes = new[]
            {
                new
                {
                    Item = new
                    {
                        Id = 0,
                        Name = "Ghost Mod",
                        Description = "Should never be saved",
                        ItemModTypeId = (int)EItemModType.Prefix,
                        RarityId = (int)ERarity.Common,
                        Attributes = Array.Empty<object>(),
                        Tags = Array.Empty<int>()
                    },
                    ChangeType = 0 // Add
                },
                new
                {
                    Item = new
                    {
                        Id = 999999,
                        Name = "Phantom",
                        Description = "x",
                        ItemModTypeId = (int)EItemModType.Prefix,
                        RarityId = (int)ERarity.Common,
                        Attributes = Array.Empty<object>(),
                        Tags = Array.Empty<int>()
                    },
                    ChangeType = 1 // Edit
                }
            };

            var response = await authClient.PostAsJsonAsync("/api/AdminTools/AddEditItemMods", changes, CancellationToken);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.NotNull(result);
            Assert.Equal("Item mod not found.", result.ErrorMessage);
            Assert.DoesNotContain(GetItemMods(), m => m.Name == "Ghost Mod");
        }

        [Fact]
        public async Task AddEditItemModAttributes_UnknownItemMod_ReturnsError()
        {
            using var authClient = await SetupAuthenticatedClientAsync();

            var data = new { Id = 999999, Changes = Array.Empty<object>() };

            var response = await authClient.PostAsJsonAsync("/api/AdminTools/AddEditItemModAttributes", data, CancellationToken);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.NotNull(result);
            Assert.Equal("Item mod not found.", result.ErrorMessage);
        }

        [Fact]
        public async Task SetSkillMultipliers_UnknownSkill_ReturnsError()
        {
            using var authClient = await SetupAuthenticatedClientAsync();

            var data = new { Id = 999999, Changes = Array.Empty<object>() };

            var response = await authClient.PostAsJsonAsync("/api/AdminTools/SetSkillMultipliers", data, CancellationToken);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.NotNull(result);
            Assert.Equal("Skill not found.", result.ErrorMessage);
        }

        [Fact]
        public async Task SetSkillMultipliers_AddAndDelete_RoundTripsThroughTheDatabase()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var skill = await TestDataSeeder.CreateSkillAsync(context, "Fireball"); // seeds Strength x1.0

            using var authClient = await SetupAuthenticatedClientAsync();

            // Add a second multiplier alongside the seeded Strength one.
            var add = new
            {
                Id = skill.Id,
                Changes = new[]
                {
                    new { Item = new { AttributeId = (int)EAttribute.Agility, Amount = 2.0m }, ChangeType = 0 } // Add
                }
            };
            var addResponse = await authClient.PostAsJsonAsync("/api/AdminTools/SetSkillMultipliers", add, CancellationToken);
            Assert.Equal(HttpStatusCode.OK, addResponse.StatusCode);

            var afterAdd = Assert.Single(GetSkills(), s => s.Id == skill.Id);
            Assert.Equal(2, afterAdd.DamageMultipliers.Count());
            var added = Assert.Single(afterAdd.DamageMultipliers, m => m.AttributeId == EAttribute.Agility);
            Assert.Equal(2.0m, added.Multiplier);

            // Delete it again, leaving only the seeded Strength multiplier.
            var delete = new
            {
                Id = skill.Id,
                Changes = new[]
                {
                    new { Item = new { AttributeId = (int)EAttribute.Agility, Amount = 2.0m }, ChangeType = 2 } // Delete
                }
            };
            var deleteResponse = await authClient.PostAsJsonAsync("/api/AdminTools/SetSkillMultipliers", delete, CancellationToken);
            Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

            var afterDelete = Assert.Single(GetSkills(), s => s.Id == skill.Id);
            Assert.Equal(EAttribute.Strength, Assert.Single(afterDelete.DamageMultipliers).AttributeId);
        }

        [Fact]
        public async Task AddEditZones_EditRetireReinstate_RoundTripsThroughTheDatabase()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var zone = await TestDataSeeder.CreateZoneAsync(context, "Original"); // Id 0

            using var authClient = await SetupAuthenticatedClientAsync();
            var before = GetZones();

            Task<HttpResponseMessage> SaveZone(DateTime? retiredAt) =>
                authClient.PostAsJsonAsync("/api/AdminTools/AddEditZones", new[]
                {
                    new
                    {
                        Item = new
                        {
                            zone.Id,
                            Name = "Renamed",
                            Description = "Updated",
                            Order = 7,
                            LevelMin = 15,
                            LevelMax = 25,
                            BossEnemyId = (int?)null,
                            BossLevel = 1,
                            UnlockChallengeId = (int?)null,
                            RetiredAt = retiredAt
                        },
                        ChangeType = 1 // Edit
                    }
                }, CancellationToken);

            Assert.Equal(HttpStatusCode.OK, (await SaveZone(null)).StatusCode);
            var after = GetZones();
            Assert.Equal(before.Count, after.Count);
            var edited = Assert.Single(after, z => z.Id == zone.Id);
            Assert.Equal("Renamed", edited.Name);
            Assert.Equal(7, edited.Order);
            Assert.Equal(15, edited.LevelMin);
            Assert.Equal(25, edited.LevelMax);
            Assert.Null(edited.RetiredAt);

            Assert.Equal(HttpStatusCode.OK, (await SaveZone(DateTime.UtcNow)).StatusCode);
            Assert.NotNull(Assert.Single(GetZones(), z => z.Id == zone.Id).RetiredAt);

            Assert.Equal(HttpStatusCode.OK, (await SaveZone(null)).StatusCode);
            Assert.Null(Assert.Single(GetZones(), z => z.Id == zone.Id).RetiredAt);
        }

        [Fact]
        public async Task AddEditZones_WithValidUnlockChallenge_PersistsGate()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var challenge = await TestDataSeeder.CreateChallengeAsync(context); // Id 0 — a valid in-range gate

            using var authClient = await SetupAuthenticatedClientAsync();

            var changes = new[]
            {
                new
                {
                    Item = new
                    {
                        Id = 0,
                        Name = "Gated Zone",
                        Description = "Sealed until the challenge is met.",
                        Order = 0,
                        LevelMin = 1,
                        LevelMax = 5,
                        BossEnemyId = (int?)null,
                        BossLevel = 1,
                        UnlockChallengeId = (int?)challenge.Id
                    },
                    ChangeType = 0 // Add
                }
            };

            var response = await authClient.PostAsJsonAsync("/api/AdminTools/AddEditZones", changes, CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var saved = Assert.Single(GetZones(), z => z.Name == "Gated Zone");
            Assert.Equal(challenge.Id, saved.UnlockChallengeId);
        }

        [Fact]
        public async Task AddEditZones_WithOutOfRangeUnlockChallenge_ReturnsErrorAndPersistsNothing()
        {
            using var authClient = await SetupAuthenticatedClientAsync();

            // No challenge with this id exists, so the in-range index check rejects the whole batch.
            var changes = new[]
            {
                new
                {
                    Item = new
                    {
                        Id = 0,
                        Name = "Phantom Gate",
                        Description = "x",
                        Order = 0,
                        LevelMin = 1,
                        LevelMax = 5,
                        BossEnemyId = (int?)null,
                        BossLevel = 1,
                        UnlockChallengeId = (int?)999999
                    },
                    ChangeType = 0 // Add
                }
            };

            var response = await authClient.PostAsJsonAsync("/api/AdminTools/AddEditZones", changes, CancellationToken);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.NotNull(result);
            Assert.Contains("unlock challenge", result.ErrorMessage ?? "", StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(GetZones(), z => z.Name == "Phantom Gate");
        }

        [Fact]
        public async Task AddEditZones_EditUnknownZone_ReturnsErrorAndPersistsNothing()
        {
            using var authClient = await SetupAuthenticatedClientAsync();

            // An identity-level edit of a non-existent zone is a not-found rejection (not a 500 from the
            // 0-row update), and the whole batch is rejected up front — so the valid Add alongside it is
            // not persisted.
            var changes = new[]
            {
                new
                {
                    Item = new
                    {
                        Id = 0,
                        Name = "Ghost Zone",
                        Description = "Should never be saved",
                        Order = 0,
                        LevelMin = 1,
                        LevelMax = 5
                    },
                    ChangeType = 0 // Add
                },
                new
                {
                    Item = new
                    {
                        Id = 999999,
                        Name = "Phantom",
                        Description = "x",
                        Order = 0,
                        LevelMin = 1,
                        LevelMax = 5
                    },
                    ChangeType = 1 // Edit
                }
            };

            var response = await authClient.PostAsJsonAsync("/api/AdminTools/AddEditZones", changes, CancellationToken);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.NotNull(result);
            Assert.Equal("Zone not found.", result.ErrorMessage);
            Assert.DoesNotContain(GetZones(), z => z.Name == "Ghost Zone");
        }

        [Fact]
        public async Task SetZoneEnemies_UnknownZone_ReturnsError()
        {
            using var authClient = await SetupAuthenticatedClientAsync();

            var data = new { ZoneId = 999999, ZoneEnemies = Array.Empty<object>() };

            var response = await authClient.PostAsJsonAsync("/api/AdminTools/SetZoneEnemies", data, CancellationToken);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.NotNull(result);
            Assert.Equal("Zone not found.", result.ErrorMessage);
        }

        [Fact]
        public async Task SetEnemySkills_UnknownEnemy_ReturnsError()
        {
            using var authClient = await SetupAuthenticatedClientAsync();

            var data = new { EnemyId = 999999, SkillIds = Array.Empty<int>() };

            var response = await authClient.PostAsJsonAsync("/api/AdminTools/SetEnemySkills", data, CancellationToken);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.NotNull(result);
            Assert.Equal("Enemy not found.", result.ErrorMessage);
        }
    }
}
