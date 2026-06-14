using Game.Abstractions.Contracts.Admin;
using Game.Abstractions.DataAccess.Admin;
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
    /// Exercises <see cref="IAdminZones.SetEnemies"/> end-to-end through the entity store and unit of work:
    /// it reconciles a zone's enemy spawns against the full desired set (the delete/update/insert logic
    /// extracted into <c>ChildCollectionReconciler</c>). Seeding, the admin write, and the assertion each
    /// use a separate DI scope so the write runs against an empty change tracker, as a real admin call does.
    /// </summary>
    [Collection("Integration")]
    public class AdminZonesIntegrationTests : ApplicationIntegrationTestBase
    {
        public AdminZonesIntegrationTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

        [Fact]
        public async Task SetEnemies_DeletesUpdatesAndInsertsAgainstDesiredSet()
        {
            int zoneId, keptEnemyId, removedEnemyId, addedEnemyId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                var zone = await TestDataSeeder.CreateZoneAsync(context);
                var keptEnemy = await TestDataSeeder.CreateEnemyAsync(context, "Kept");
                var removedEnemy = await TestDataSeeder.CreateEnemyAsync(context, "Removed");
                var addedEnemy = await TestDataSeeder.CreateEnemyAsync(context, "Added");
                await TestDataSeeder.LinkEnemyToZoneAsync(context, zone.Id, keptEnemy.Id, weight: 1);
                await TestDataSeeder.LinkEnemyToZoneAsync(context, zone.Id, removedEnemy.Id, weight: 5);

                zoneId = zone.Id;
                keptEnemyId = keptEnemy.Id;
                removedEnemyId = removedEnemy.Id;
                addedEnemyId = addedEnemy.Id;
            }
            await ReloadReferenceCachesAsync();

            // Update the kept enemy's weight, insert the added enemy, drop the removed enemy.
            var data = new SetZoneEnemiesData
            {
                ZoneId = zoneId,
                ZoneEnemies =
                [
                    new Contracts.ZoneEnemy { EnemyId = keptEnemyId, Weight = 20 },
                    new Contracts.ZoneEnemy { EnemyId = addedEnemyId, Weight = 30 },
                ],
            };

            using (var writeScope = CreateScope())
            {
                var admin = writeScope.ServiceProvider.GetRequiredService<IAdminZones>();
                Assert.True(admin.SetEnemies(data));
                await writeScope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync();
            }

            using (var assertScope = CreateScope())
            {
                var context = assertScope.ServiceProvider.GetRequiredService<GameContext>();
                var spawns = await context.Set<Entities.ZoneEnemy>()
                    .Where(ze => ze.ZoneId == zoneId)
                    .ToListAsync(CancellationToken);

                Assert.Equal(2, spawns.Count);
                Assert.DoesNotContain(spawns, ze => ze.EnemyId == removedEnemyId);
                Assert.Equal(20, spawns.Single(ze => ze.EnemyId == keptEnemyId).Weight);
                Assert.Equal(30, spawns.Single(ze => ze.EnemyId == addedEnemyId).Weight);
            }
        }

        [Fact]
        public void SetEnemies_UnknownZone_ReturnsFalse()
        {
            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminZones>();

            var result = admin.SetEnemies(new SetZoneEnemiesData { ZoneId = 99999, ZoneEnemies = [] });

            Assert.False(result);
        }
    }
}
