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
    /// Exercises the three <see cref="IAdminEnemies"/> relationship setters end-to-end through the entity
    /// store and unit of work. Each reconciles a child collection against the full desired set the admin
    /// submits — the delete/update/insert logic extracted into <c>ChildCollectionReconciler</c>. Seeding,
    /// the admin write, and the assertion each use a separate DI scope so the write runs against an empty
    /// change tracker, mirroring the per-request scope of a real admin call.
    /// </summary>
    [Collection("Integration")]
    public class AdminEnemiesIntegrationTests : ApplicationIntegrationTestBase
    {
        public AdminEnemiesIntegrationTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

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
                Assert.True(admin.SetAttributeDistributions(data));
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
                Assert.True(admin.SetSkills(data));
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
                Assert.True(admin.SetSpawns(data));
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
        public void SetAttributeDistributions_UnknownEnemy_ReturnsFalse()
        {
            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminEnemies>();

            var result = admin.SetAttributeDistributions(new SetEnemyAttributeDistributions
            {
                EnemyId = 99999,
                AttributeDistributions = [],
            });

            Assert.False(result);
        }

        [Fact]
        public void SetSkills_UnknownEnemy_ReturnsFalse()
        {
            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminEnemies>();

            var result = admin.SetSkills(new SetEnemySkillsData { EnemyId = 99999, SkillIds = [] });

            Assert.False(result);
        }

        [Fact]
        public void SetSpawns_UnknownEnemy_ReturnsFalse()
        {
            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminEnemies>();

            var result = admin.SetSpawns(new SetEnemySpawnsData { EnemyId = 99999, Spawns = [] });

            Assert.False(result);
        }
    }
}
