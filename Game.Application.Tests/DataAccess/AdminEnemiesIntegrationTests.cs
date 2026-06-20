using Game.Abstractions.Contracts.Admin;
using Game.Abstractions.DataAccess.Admin;
using Game.Core;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Base;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Contracts = Game.Abstractions.Contracts;
using Entities = Game.Infrastructure.Entities;

namespace Game.Application.Tests.DataAccess
{
    /// <summary>
    /// Exercises <see cref="IAdminEnemies"/> end-to-end through the entity store and unit of work: the
    /// enemy change-set edit path plus the three relationship setters. Each setter reconciles a child
    /// collection against the full desired set the admin submits — the delete/update/insert logic extracted
    /// into <c>ChildCollectionReconciler</c>. The edit path also pins the zero-based-identity save fixup for
    /// record 0 (Id == 0, the identity seed). Seeding, the admin write, and the assertion each use a separate
    /// DI scope so the write runs against an empty change tracker, mirroring the per-request scope of a real
    /// admin call.
    /// </summary>
    [Collection("Integration")]
    public class AdminEnemiesIntegrationTests : ApplicationIntegrationTestBase
    {
        public AdminEnemiesIntegrationTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

        [Fact]
        public async Task SaveEnemies_EditsRecordZero_UpdatesTheCorrectRow()
        {
            // Record 0 is the identity seed of the zero-based Enemy set, so its Id == default(int). EF can read
            // that as an unset store-generated value; the zero-based-identity save fixup (GameContext, #1003)
            // guards against EF assigning the edit a temporary key that would target the wrong row. The DB is
            // truncated with RESTART IDENTITY before each test, so the first seeded enemy lands at Id 0 and the
            // second at Id 1 — editing 0 must change row 0 only and leave row 1 untouched.
            int recordZeroId, neighborId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                var recordZero = await TestDataSeeder.CreateEnemyAsync(context, "Original Zero");
                var neighbor = await TestDataSeeder.CreateEnemyAsync(context, "Neighbor One");
                recordZeroId = recordZero.Id;
                neighborId = neighbor.Id;
            }
            await ReloadReferenceCachesAsync();

            Assert.Equal(0, recordZeroId);
            Assert.Equal(1, neighborId);

            var changes = new List<Change<Contracts.Enemy>>
            {
                new()
                {
                    ChangeType = EChangeType.Edit,
                    Item = new Contracts.Enemy
                    {
                        Id = recordZeroId,
                        Name = "Edited Zero",
                        IsBoss = true,
                        AttributeDistribution = [],
                        SkillPool = [],
                        Spawns = [],
                    },
                },
            };

            using (var writeScope = CreateScope())
            {
                var admin = writeScope.ServiceProvider.GetRequiredService<IAdminEnemies>();
                Assert.True(admin.SaveEnemies(changes).Succeeded);
                await writeScope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync();
            }

            using (var assertScope = CreateScope())
            {
                var context = assertScope.ServiceProvider.GetRequiredService<GameContext>();

                var edited = await context.Enemies.SingleAsync(e => e.Id == recordZeroId, CancellationToken);
                Assert.Equal("Edited Zero", edited.Name);
                Assert.True(edited.IsBoss);

                // The neighbor proves the UPDATE was scoped to record 0, not broadened to every row by a
                // mis-resolved (temporary / default) key.
                var neighbor = await context.Enemies.SingleAsync(e => e.Id == neighborId, CancellationToken);
                Assert.Equal("Neighbor One", neighbor.Name);
                Assert.False(neighbor.IsBoss);
            }
        }

        [Fact]
        public async Task SetAttributeDistributions_DeletesUpdatesAndInsertsAgainstDesiredSet()
        {
            int enemyId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                // CreateEnemyAsync seeds Strength + Endurance distributions.
                var enemy = await TestDataSeeder.CreateEnemyAsync(context);
                enemyId = enemy.Id;
            }
            await ReloadReferenceCachesAsync();

            // Update Strength, insert Intellect, drop Endurance (omitted from the desired set).
            var data = new SetEnemyAttributeDistributions
            {
                EnemyId = enemyId,
                AttributeDistributions =
                [
                    new Contracts.AttributeDistribution { AttributeId = EAttribute.Strength, BaseAmount = 99m, AmountPerLevel = 9m },
                    new Contracts.AttributeDistribution { AttributeId = EAttribute.Intellect, BaseAmount = 3m, AmountPerLevel = 2m },
                ],
            };

            using (var writeScope = CreateScope())
            {
                var admin = writeScope.ServiceProvider.GetRequiredService<IAdminEnemies>();
                Assert.True(admin.SetAttributeDistributions(data).Succeeded);
                await writeScope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync();
            }

            using (var assertScope = CreateScope())
            {
                var context = assertScope.ServiceProvider.GetRequiredService<GameContext>();
                var distributions = await context.AttributeDistributions
                    .Where(ad => ad.EnemyId == enemyId)
                    .ToListAsync(CancellationToken);

                Assert.Equal(2, distributions.Count);
                Assert.DoesNotContain(distributions, ad => ad.AttributeId == (int)EAttribute.Endurance);

                var strength = distributions.Single(ad => ad.AttributeId == (int)EAttribute.Strength);
                Assert.Equal(99m, strength.BaseAmount);
                Assert.Equal(9m, strength.AmountPerLevel);

                var intellect = distributions.Single(ad => ad.AttributeId == (int)EAttribute.Intellect);
                Assert.Equal(3m, intellect.BaseAmount);
                Assert.Equal(2m, intellect.AmountPerLevel);
            }
        }

        [Fact]
        public async Task SetSkills_DeletesRemovedAndInsertsNew_LeavingUnchangedJoinRows()
        {
            int enemyId, keptSkillId, removedSkillId, addedSkillId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                var enemy = await TestDataSeeder.CreateEnemyAsync(context);
                var kept = await TestDataSeeder.CreateSkillAsync(context, "Kept");
                var removed = await TestDataSeeder.CreateSkillAsync(context, "Removed");
                var added = await TestDataSeeder.CreateSkillAsync(context, "Added");
                await TestDataSeeder.LinkSkillToEnemyAsync(context, enemy.Id, kept.Id);
                await TestDataSeeder.LinkSkillToEnemyAsync(context, enemy.Id, removed.Id);

                enemyId = enemy.Id;
                keptSkillId = kept.Id;
                removedSkillId = removed.Id;
                addedSkillId = added.Id;
            }
            await ReloadReferenceCachesAsync();

            var data = new SetEnemySkillsData { EnemyId = enemyId, SkillIds = [keptSkillId, addedSkillId] };

            using (var writeScope = CreateScope())
            {
                var admin = writeScope.ServiceProvider.GetRequiredService<IAdminEnemies>();
                Assert.True(admin.SetSkills(data).Succeeded);
                await writeScope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync();
            }

            using (var assertScope = CreateScope())
            {
                var context = assertScope.ServiceProvider.GetRequiredService<GameContext>();
                var skillIds = await context.EnemySkills
                    .Where(es => es.EnemyId == enemyId)
                    .Select(es => es.SkillId)
                    .ToListAsync(CancellationToken);

                Assert.Equal([keptSkillId, addedSkillId], skillIds.OrderBy(id => id));
                Assert.DoesNotContain(removedSkillId, skillIds);
            }
        }

        [Fact]
        public async Task SetSpawns_DeletesUpdatesAndInsertsAgainstDesiredSet()
        {
            int enemyId, keptZoneId, removedZoneId, addedZoneId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                var enemy = await TestDataSeeder.CreateEnemyAsync(context);
                var keptZone = await TestDataSeeder.CreateZoneAsync(context, "Kept");
                var removedZone = await TestDataSeeder.CreateZoneAsync(context, "Removed");
                var addedZone = await TestDataSeeder.CreateZoneAsync(context, "Added");
                await TestDataSeeder.LinkEnemyToZoneAsync(context, keptZone.Id, enemy.Id, weight: 1);
                await TestDataSeeder.LinkEnemyToZoneAsync(context, removedZone.Id, enemy.Id, weight: 5);

                enemyId = enemy.Id;
                keptZoneId = keptZone.Id;
                removedZoneId = removedZone.Id;
                addedZoneId = addedZone.Id;
            }
            await ReloadReferenceCachesAsync();

            // Update the kept zone's weight, insert the added zone, drop the removed zone.
            var data = new SetEnemySpawnsData
            {
                EnemyId = enemyId,
                Spawns =
                [
                    new Contracts.EnemySpawn { ZoneId = keptZoneId, Weight = 20 },
                    new Contracts.EnemySpawn { ZoneId = addedZoneId, Weight = 30 },
                ],
            };

            using (var writeScope = CreateScope())
            {
                var admin = writeScope.ServiceProvider.GetRequiredService<IAdminEnemies>();
                Assert.True(admin.SetSpawns(data).Succeeded);
                await writeScope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync();
            }

            using (var assertScope = CreateScope())
            {
                var context = assertScope.ServiceProvider.GetRequiredService<GameContext>();
                var spawns = await context.Set<Entities.ZoneEnemy>()
                    .Where(ze => ze.EnemyId == enemyId)
                    .ToListAsync(CancellationToken);

                Assert.Equal(2, spawns.Count);
                Assert.DoesNotContain(spawns, ze => ze.ZoneId == removedZoneId);
                Assert.Equal(20, spawns.Single(ze => ze.ZoneId == keptZoneId).Weight);
                Assert.Equal(30, spawns.Single(ze => ze.ZoneId == addedZoneId).Weight);
            }
        }

        [Fact]
        public async Task SetAttributeDistributions_DuplicateDesiredKeys_ReturnsFailure()
        {
            int enemyId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                var enemy = await TestDataSeeder.CreateEnemyAsync(context);
                enemyId = enemy.Id;
            }
            await ReloadReferenceCachesAsync();

            // The same attribute named twice in the desired set would otherwise double-insert into a
            // composite-PK violation at commit; it must reject up front instead.
            var data = new SetEnemyAttributeDistributions
            {
                EnemyId = enemyId,
                AttributeDistributions =
                [
                    new Contracts.AttributeDistribution { AttributeId = EAttribute.Intellect, BaseAmount = 1m, AmountPerLevel = 1m },
                    new Contracts.AttributeDistribution { AttributeId = EAttribute.Intellect, BaseAmount = 2m, AmountPerLevel = 2m },
                ],
            };

            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminEnemies>();

            var result = admin.SetAttributeDistributions(data);

            Assert.False(result.Succeeded);
            Assert.Equal("The submitted attribute distribution set contains duplicate entries.", result.ErrorMessage);
        }

        [Fact]
        public async Task SaveEnemies_DuplicateEditKeysInBatch_ReturnsFailureWithoutThrowing()
        {
            int enemyId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                var enemy = await TestDataSeeder.CreateEnemyAsync(context);
                enemyId = enemy.Id;
            }
            await ReloadReferenceCachesAsync();

            // Two Edits of the same id would double-track the entity and surface as an opaque EF 500 mid-batch;
            // the identity-level change-set processor must reject the malformed batch up front as a graceful
            // failure (the same standard the reconciler already holds).
            Contracts.Enemy EnemyEdit(string name) => new()
            {
                Id = enemyId,
                Name = name,
                IsBoss = false,
                AttributeDistribution = [],
                SkillPool = [],
                Spawns = [],
            };

            var changes = new[]
            {
                new Change<Contracts.Enemy> { ChangeType = EChangeType.Edit, Item = EnemyEdit("First") },
                new Change<Contracts.Enemy> { ChangeType = EChangeType.Edit, Item = EnemyEdit("Second") },
            };

            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminEnemies>();

            var result = admin.SaveEnemies(changes);

            Assert.False(result.Succeeded);
            Assert.Equal("The submitted enemy change set contains duplicate entries.", result.ErrorMessage);
        }

        [Fact]
        public void SetAttributeDistributions_UnknownEnemy_ReturnsNotFound()
        {
            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminEnemies>();

            var result = admin.SetAttributeDistributions(new SetEnemyAttributeDistributions
            {
                EnemyId = 99999,
                AttributeDistributions = [],
            });

            Assert.False(result.Succeeded);
            Assert.Equal("Enemy not found.", result.ErrorMessage);
        }

        [Fact]
        public void SetSkills_UnknownEnemy_ReturnsNotFound()
        {
            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminEnemies>();

            var result = admin.SetSkills(new SetEnemySkillsData { EnemyId = 99999, SkillIds = [] });

            Assert.False(result.Succeeded);
            Assert.Equal("Enemy not found.", result.ErrorMessage);
        }

        [Fact]
        public void SetSpawns_UnknownEnemy_ReturnsNotFound()
        {
            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminEnemies>();

            var result = admin.SetSpawns(new SetEnemySpawnsData { EnemyId = 99999, Spawns = [] });

            Assert.False(result.Succeeded);
            Assert.Equal("Enemy not found.", result.ErrorMessage);
        }

        [Fact]
        public async Task SaveEnemies_RetiringLastActiveEnemyOfLiveZone_ReturnsFailure()
        {
            int enemyId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                var enemy = await TestDataSeeder.CreateEnemyAsync(context, "Lone Goblin");
                var zone = await TestDataSeeder.CreateZoneAsync(context, "Forest");
                await TestDataSeeder.LinkEnemyToZoneAsync(context, zone.Id, enemy.Id);
                enemyId = enemy.Id;
            }
            await ReloadReferenceCachesAsync();

            // Retiring the only spawn enemy of a live zone would drop that zone from the spawn tables and
            // throw at the next idle encounter, so it must be rejected at save time.
            var changes = new[] { RetireEnemyChange(enemyId, "Lone Goblin") };

            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminEnemies>();

            var result = admin.SaveEnemies(changes);

            Assert.False(result.Succeeded);
            Assert.Equal(
                "Retiring 'Lone Goblin' would leave live zone 'Forest' with no spawnable enemies. "
                    + "Retire the zone first, or keep at least one active enemy spawning in it.",
                result.ErrorMessage);

            // The rejection is up front, before anything is staged: committing the unit of work persists no
            // retirement, so the enemy stays in circulation.
            using var assertScope = CreateScope();
            var assertContext = assertScope.ServiceProvider.GetRequiredService<GameContext>();
            var persisted = await assertContext.Enemies.SingleAsync(e => e.Id == enemyId, CancellationToken);
            Assert.Null(persisted.RetiredAt);
        }

        [Fact]
        public async Task SaveEnemies_RetiringLastEnemyAfterZoneRetired_Succeeds()
        {
            int enemyId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                var enemy = await TestDataSeeder.CreateEnemyAsync(context, "Lone Goblin");
                // The zone is already retired (out of circulation), so its occupants are relocated by the
                // runtime safety net — retiring its last enemy is allowed.
                var zone = await TestDataSeeder.CreateZoneAsync(context, "Forest", retiredAt: DateTime.UtcNow);
                await TestDataSeeder.LinkEnemyToZoneAsync(context, zone.Id, enemy.Id);
                enemyId = enemy.Id;
            }
            await ReloadReferenceCachesAsync();

            var changes = new[] { RetireEnemyChange(enemyId, "Lone Goblin") };

            using (var writeScope = CreateScope())
            {
                var admin = writeScope.ServiceProvider.GetRequiredService<IAdminEnemies>();
                Assert.True(admin.SaveEnemies(changes).Succeeded);
                await writeScope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync();
            }

            using var assertScope = CreateScope();
            var assertContext = assertScope.ServiceProvider.GetRequiredService<GameContext>();
            var persisted = await assertContext.Enemies.SingleAsync(e => e.Id == enemyId, CancellationToken);
            Assert.NotNull(persisted.RetiredAt);
        }

        [Fact]
        public async Task SaveEnemies_RetiringEnemyWhenZoneKeepsAnotherActiveEnemy_Succeeds()
        {
            int retiredEnemyId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                var toRetire = await TestDataSeeder.CreateEnemyAsync(context, "First Goblin");
                var survivor = await TestDataSeeder.CreateEnemyAsync(context, "Second Goblin");
                var zone = await TestDataSeeder.CreateZoneAsync(context, "Forest");
                // Both enemies spawn in the live zone, so retiring one still leaves a spawnable enemy.
                await TestDataSeeder.LinkEnemyToZoneAsync(context, zone.Id, toRetire.Id);
                await TestDataSeeder.LinkEnemyToZoneAsync(context, zone.Id, survivor.Id);
                retiredEnemyId = toRetire.Id;
            }
            await ReloadReferenceCachesAsync();

            var changes = new[] { RetireEnemyChange(retiredEnemyId, "First Goblin") };

            using (var writeScope = CreateScope())
            {
                var admin = writeScope.ServiceProvider.GetRequiredService<IAdminEnemies>();
                Assert.True(admin.SaveEnemies(changes).Succeeded);
                await writeScope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync();
            }

            using var assertScope = CreateScope();
            var assertContext = assertScope.ServiceProvider.GetRequiredService<GameContext>();
            var persisted = await assertContext.Enemies.SingleAsync(e => e.Id == retiredEnemyId, CancellationToken);
            Assert.NotNull(persisted.RetiredAt);
        }

        [Fact]
        public async Task SaveEnemies_RetiringEnemyWithNoZoneSpawns_Succeeds()
        {
            int enemyId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                // An enemy assigned to no zone cannot strand a zone, so the guard does not apply.
                var enemy = await TestDataSeeder.CreateEnemyAsync(context, "Unassigned Goblin");
                enemyId = enemy.Id;
            }
            await ReloadReferenceCachesAsync();

            var changes = new[] { RetireEnemyChange(enemyId, "Unassigned Goblin") };

            using (var writeScope = CreateScope())
            {
                var admin = writeScope.ServiceProvider.GetRequiredService<IAdminEnemies>();
                Assert.True(admin.SaveEnemies(changes).Succeeded);
                await writeScope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync();
            }

            using var assertScope = CreateScope();
            var assertContext = assertScope.ServiceProvider.GetRequiredService<GameContext>();
            var persisted = await assertContext.Enemies.SingleAsync(e => e.Id == enemyId, CancellationToken);
            Assert.NotNull(persisted.RetiredAt);
        }

        // An Edit change that retires the enemy (sets RetiredAt). Mirrors the whole-record edit the admin
        // Workbench sends — identity plus the now-retired flag, with empty related collections.
        private static Change<Contracts.Enemy> RetireEnemyChange(int enemyId, string name) => new()
        {
            ChangeType = EChangeType.Edit,
            Item = new Contracts.Enemy
            {
                Id = enemyId,
                Name = name,
                IsBoss = false,
                RetiredAt = DateTime.UtcNow,
                AttributeDistribution = [],
                SkillPool = [],
                Spawns = [],
            },
        };
    }
}
