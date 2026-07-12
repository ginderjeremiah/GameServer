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
    /// extracted into <c>ChildCollectionReconciler</c>), plus the <see cref="IAdminZones.SaveZones"/>
    /// edit-existence rejection (now the change-set processor's shared guard). Seeding, the admin write, and
    /// the assertion each use a separate DI scope so the write runs against an empty change tracker, as a real
    /// admin call does.
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
                Assert.True(admin.SetEnemies(data).Succeeded);
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
        public async Task SetEnemies_DuplicateDesiredKeys_ReturnsFailure()
        {
            int zoneId, enemyId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                var zone = await TestDataSeeder.CreateZoneAsync(context);
                var enemy = await TestDataSeeder.CreateEnemyAsync(context);
                zoneId = zone.Id;
                enemyId = enemy.Id;
            }
            await ReloadReferenceCachesAsync();

            // The same enemy named twice in the desired set would otherwise double-insert into a
            // composite-PK violation at commit; it must reject up front instead.
            var data = new SetZoneEnemiesData
            {
                ZoneId = zoneId,
                ZoneEnemies =
                [
                    new Contracts.ZoneEnemy { EnemyId = enemyId, Weight = 1 },
                    new Contracts.ZoneEnemy { EnemyId = enemyId, Weight = 2 },
                ],
            };

            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminZones>();

            var result = admin.SetEnemies(data);

            Assert.False(result.Succeeded);
            Assert.Equal("The submitted enemy change set contains duplicate entries.", result.ErrorMessage);
        }

        [Fact]
        public async Task SetEnemies_NegativeWeight_ReturnsFailure()
        {
            int zoneId, enemyId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                var zone = await TestDataSeeder.CreateZoneAsync(context);
                var enemy = await TestDataSeeder.CreateEnemyAsync(context);
                zoneId = zone.Id;
                enemyId = enemy.Id;
            }
            await ReloadReferenceCachesAsync();

            // A negative weight would otherwise commit and then throw inside ProbabilityTable's constructor
            // when the enemy snapshot next rebuilds — reject it up front instead.
            var data = new SetZoneEnemiesData
            {
                ZoneId = zoneId,
                ZoneEnemies = [new Contracts.ZoneEnemy { EnemyId = enemyId, Weight = -1 }],
            };

            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminZones>();

            var result = admin.SetEnemies(data);

            Assert.False(result.Succeeded);
            Assert.Equal("A zone enemy's spawn weight cannot be negative.", result.ErrorMessage);
        }

        [Fact]
        public void SetEnemies_UnknownZone_ReturnsNotFound()
        {
            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminZones>();

            var result = admin.SetEnemies(new SetZoneEnemiesData { ZoneId = 99999, ZoneEnemies = [] });

            Assert.False(result.Succeeded);
            Assert.Equal("Zone not found.", result.ErrorMessage);
        }

        [Fact]
        public async Task SetEnemies_HomeZone_ReturnsFailure()
        {
            int homeZoneId, enemyId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                var home = await TestDataSeeder.CreateZoneAsync(context, "Home", isHome: true);
                var enemy = await TestDataSeeder.CreateEnemyAsync(context);
                homeZoneId = home.Id;
                enemyId = enemy.Id;
            }
            await ReloadReferenceCachesAsync();

            // The Home zone is a no-combat sanctuary, so assigning it a spawn table is rejected outright.
            var data = new SetZoneEnemiesData
            {
                ZoneId = homeZoneId,
                ZoneEnemies = [new Contracts.ZoneEnemy { EnemyId = enemyId, Weight = 1 }],
            };

            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminZones>();

            var result = admin.SetEnemies(data);

            Assert.False(result.Succeeded);
            Assert.Equal(
                "The Home zone cannot have enemy spawns. Home is a no-combat sanctuary where no enemies spawn.",
                result.ErrorMessage);
        }

        [Fact]
        public async Task SaveZones_HomeZoneWithBoss_ReturnsFailure()
        {
            int bossEnemyId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                bossEnemyId = (await TestDataSeeder.CreateEnemyAsync(context, "Boss", isBoss: true)).Id;
            }
            await ReloadReferenceCachesAsync();

            // A Home zone declaring a (valid) boss is rejected: Home spawns no enemies, and a boss is the
            // non-random enemy source. The boss reference itself is valid, so this exercises the Home guard.
            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminZones>();

            var result = admin.SaveZones(
            [
                new Change<Contracts.Zone>
                {
                    ChangeType = EChangeType.Add,
                    Item = new Contracts.Zone
                    {
                        Name = "Home",
                        Description = "",
                        DesignerNotes = "",
                        LevelMin = 1,
                        LevelMax = 1,
                        BossLevel = 1,
                        BossEnemyId = bossEnemyId,
                        IsHome = true,
                    },
                },
            ]);

            Assert.False(result.Succeeded);
            Assert.Equal(
                "The Home zone cannot have a boss. Home is a no-combat sanctuary where no enemies spawn.",
                result.ErrorMessage);
        }

        [Fact]
        public async Task SaveZones_AddsFirstHomeZone_PersistsIsHome()
        {
            // No Home zone exists yet, so authoring the first one is allowed — and IsHome must round-trip to
            // the entity (the authoring path persists the sanctuary flag, not just the read contract).
            using (var writeScope = CreateScope())
            {
                var admin = writeScope.ServiceProvider.GetRequiredService<IAdminZones>();
                var result = admin.SaveZones(
                [
                    new Change<Contracts.Zone>
                    {
                        ChangeType = EChangeType.Add,
                        Item = new Contracts.Zone
                        {
                            Name = "Home",
                            Description = "A quiet refuge.",
                            DesignerNotes = "",
                            LevelMin = 1,
                            LevelMax = 1,
                            BossLevel = 1,
                            IsHome = true,
                        },
                    },
                ]);

                Assert.True(result.Succeeded);
                await writeScope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync();
            }

            using (var assertScope = CreateScope())
            {
                var context = assertScope.ServiceProvider.GetRequiredService<GameContext>();
                var zone = await context.Set<Entities.Zone>().SingleAsync(CancellationToken);
                Assert.True(zone.IsHome);
            }
        }

        [Fact]
        public async Task SaveZones_SecondHomeZone_ReturnsFailure()
        {
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                await TestDataSeeder.CreateZoneAsync(context, "Home", isHome: true);
            }
            await ReloadReferenceCachesAsync();

            // A Home zone already exists, so adding a second one would leave two non-retired sanctuaries —
            // rejected outright by the single-Home authoring guard.
            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminZones>();

            var result = admin.SaveZones(
            [
                new Change<Contracts.Zone>
                {
                    ChangeType = EChangeType.Add,
                    Item = new Contracts.Zone
                    {
                        Name = "Second Home",
                        Description = "",
                        DesignerNotes = "",
                        LevelMin = 1,
                        LevelMax = 1,
                        BossLevel = 1,
                        IsHome = true,
                    },
                },
            ]);

            Assert.False(result.Succeeded);
            Assert.Equal(
                "Only one Home zone is allowed. Another non-retired Home zone already exists; retire it first or make this a combat zone.",
                result.ErrorMessage);
        }

        [Fact]
        public async Task SaveZones_FlipExistingCombatZoneToHome_WhenHomeExists_ReturnsFailure()
        {
            int combatZoneId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                await TestDataSeeder.CreateZoneAsync(context, "Home", isHome: true);
                combatZoneId = (await TestDataSeeder.CreateZoneAsync(context, "Wilds")).Id;
            }
            await ReloadReferenceCachesAsync();

            // Flipping an existing combat zone to Home is the second way to end up with two sanctuaries; the
            // guard reasons over the full post-save catalogue, so it catches the edit too.
            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminZones>();

            var result = admin.SaveZones(
            [
                new Change<Contracts.Zone>
                {
                    ChangeType = EChangeType.Edit,
                    Item = new Contracts.Zone
                    {
                        Id = combatZoneId,
                        Name = "Wilds",
                        Description = "",
                        DesignerNotes = "",
                        LevelMin = 1,
                        LevelMax = 10,
                        BossLevel = 1,
                        IsHome = true,
                    },
                },
            ]);

            Assert.False(result.Succeeded);
            Assert.Equal(
                "Only one Home zone is allowed. Another non-retired Home zone already exists; retire it first or make this a combat zone.",
                result.ErrorMessage);
        }

        [Fact]
        public async Task SaveZones_FlipZoneToHomeWithSpawns_ReturnsFailure()
        {
            int combatZoneId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                var zone = await TestDataSeeder.CreateZoneAsync(context, "Wilds");
                var enemy = await TestDataSeeder.CreateEnemyAsync(context);
                await TestDataSeeder.LinkEnemyToZoneAsync(context, zone.Id, enemy.Id, weight: 1);
                combatZoneId = zone.Id;
            }
            await ReloadReferenceCachesAsync();

            // Flipping an already-spawn-populated combat zone to Home is the third way to leave the
            // sanctuary with live spawns (the other two — a Home boss, and SetEnemies against a Home zone —
            // are already guarded). The guard reasons over the zone's currently cached spawn table, not this
            // save's own payload, since SaveZones never carries spawn data.
            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminZones>();

            var result = admin.SaveZones(
            [
                new Change<Contracts.Zone>
                {
                    ChangeType = EChangeType.Edit,
                    Item = new Contracts.Zone
                    {
                        Id = combatZoneId,
                        Name = "Wilds",
                        Description = "",
                        DesignerNotes = "",
                        LevelMin = 1,
                        LevelMax = 10,
                        BossLevel = 1,
                        IsHome = true,
                    },
                },
            ]);

            Assert.False(result.Succeeded);
            Assert.Equal(
                "The Home zone cannot have enemy spawns. Home is a no-combat sanctuary where no enemies spawn; clear the zone's spawn table before making it Home.",
                result.ErrorMessage);
        }

        [Fact]
        public async Task SaveZones_RetiredHomeZone_DoesNotBlockNewHome()
        {
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                await TestDataSeeder.CreateZoneAsync(context, "Old Home", isHome: true, retiredAt: DateTime.UtcNow);
            }
            await ReloadReferenceCachesAsync();

            // A retired Home zone is out of circulation, so it doesn't count toward the single-Home limit —
            // authoring a fresh sanctuary is allowed.
            using (var writeScope = CreateScope())
            {
                var admin = writeScope.ServiceProvider.GetRequiredService<IAdminZones>();
                var result = admin.SaveZones(
                [
                    new Change<Contracts.Zone>
                    {
                        ChangeType = EChangeType.Add,
                        Item = new Contracts.Zone
                        {
                            Name = "New Home",
                            Description = "",
                            DesignerNotes = "",
                            LevelMin = 1,
                            LevelMax = 1,
                            BossLevel = 1,
                            IsHome = true,
                        },
                    },
                ]);

                Assert.True(result.Succeeded);
                await writeScope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync();
            }

            using (var assertScope = CreateScope())
            {
                var context = assertScope.ServiceProvider.GetRequiredService<GameContext>();
                var activeHomeCount = await context.Set<Entities.Zone>()
                    .CountAsync(z => z.IsHome && z.RetiredAt == null, CancellationToken);
                Assert.Equal(1, activeHomeCount);
            }
        }

        [Fact]
        public void SaveZones_EditUnknownZone_ReturnsNotFound()
        {
            // An Edit of a non-existent zone (no boss/unlock references, so the FK pre-pass passes) must be
            // rejected by the processor's shared edit-existence guard rather than reaching a 0-row UPDATE.
            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminZones>();

            var result = admin.SaveZones(
            [
                new Change<Contracts.Zone>
                {
                    ChangeType = EChangeType.Edit,
                    Item = new Contracts.Zone { Id = 99999, Name = "Ghost", Description = "", DesignerNotes = "" },
                },
            ]);

            Assert.False(result.Succeeded);
            Assert.Equal("Zone not found.", result.ErrorMessage);
        }
    }
}
