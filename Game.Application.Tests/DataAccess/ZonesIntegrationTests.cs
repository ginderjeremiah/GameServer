using Game.Abstractions.DataAccess;
using Game.DataAccess.Repositories;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Base;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Game.Application.Tests.DataAccess
{
    /// <summary>
    /// Verifies the <see cref="IZones"/> read paths — <see cref="IZones.GetDomainZone"/>'s bounds check and
    /// domain-model projection, and the not-found behaviour of <see cref="IZoneEntityCache.LookupZone"/>
    /// (returns <c>null</c>). The read-contract field mapping itself is covered by <c>ZoneMapperTests</c>.
    /// </summary>
    [Collection("Integration")]
    public class ZonesIntegrationTests : ApplicationIntegrationTestBase
    {
        public ZonesIntegrationTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

        [Fact]
        public async Task GetDomainZone_ReturnsZoneAsDomainModelWithBossFields()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var boss = await TestDataSeeder.CreateEnemyAsync(context, "Catacomb Lich", isBoss: true);
            var zone = await TestDataSeeder.CreateZoneAsync(
                context, "Forgotten Catacombs", levelMin: 8, levelMax: 11, bossEnemyId: boss.Id, bossLevel: 18);
            await ReloadReferenceCachesAsync();

            var zones = scope.ServiceProvider.GetRequiredService<IZones>();

            var result = zones.GetDomainZone(zone.Id);

            Assert.Equal(zone.Id, result.Id);
            Assert.Equal(8, result.LevelMin);
            Assert.Equal(11, result.LevelMax);
            Assert.Equal(boss.Id, result.BossEnemyId);
            Assert.Equal(18, result.BossLevel);
        }

        [Fact]
        public async Task GetDomainZone_WithoutBoss_HasNoBoss()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var zone = await TestDataSeeder.CreateZoneAsync(context, "Bossless Glade");
            await ReloadReferenceCachesAsync();

            var zones = scope.ServiceProvider.GetRequiredService<IZones>();

            var result = zones.GetDomainZone(zone.Id);

            Assert.Null(result.BossEnemyId);
        }

        [Fact]
        public void GetDomainZone_InvalidId_Throws()
        {
            using var scope = CreateScope();
            var zones = scope.ServiceProvider.GetRequiredService<IZones>();

            Assert.Throws<ArgumentOutOfRangeException>(() => zones.GetDomainZone(99999));
        }

        [Fact]
        public void LookupZone_InvalidId_ReturnsNull()
        {
            using var scope = CreateScope();
            var zones = scope.ServiceProvider.GetRequiredService<IZoneEntityCache>();

            // LookupZone (the admin/data-tier entity lookup) reports a missing zone as null — matching the
            // LookupItemMod convention — so AdminZones.SetEnemies can return false rather than throwing.
            Assert.Null(zones.LookupZone(99999));
        }

        [Fact]
        public async Task IsZoneRetired_ReflectsTheCatalogueRetirementFlag()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var active = await TestDataSeeder.CreateZoneAsync(context, "Active Zone", order: 0);
            var retired = await TestDataSeeder.CreateZoneAsync(context, "Retired Zone", order: 1, retiredAt: DateTime.UtcNow);
            await ReloadReferenceCachesAsync();

            var zones = scope.ServiceProvider.GetRequiredService<IZones>();

            Assert.False(zones.IsZoneRetired(active.Id));
            Assert.True(zones.IsZoneRetired(retired.Id));
        }

        [Fact]
        public void IsZoneRetired_InvalidId_Throws()
        {
            using var scope = CreateScope();
            var zones = scope.ServiceProvider.GetRequiredService<IZones>();

            Assert.Throws<ArgumentOutOfRangeException>(() => zones.IsZoneRetired(99999));
        }

        [Fact]
        public async Task IsHomeZone_ReflectsTheCatalogueHomeFlag()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var combat = await TestDataSeeder.CreateZoneAsync(context, "Combat Zone", order: 0);
            var home = await TestDataSeeder.CreateZoneAsync(context, "Home", order: 1, isHome: true);
            await ReloadReferenceCachesAsync();

            var zones = scope.ServiceProvider.GetRequiredService<IZones>();

            Assert.False(zones.IsHomeZone(combat.Id));
            Assert.True(zones.IsHomeZone(home.Id));
        }

        [Fact]
        public void IsHomeZone_InvalidId_Throws()
        {
            using var scope = CreateScope();
            var zones = scope.ServiceProvider.GetRequiredService<IZones>();

            Assert.Throws<ArgumentOutOfRangeException>(() => zones.IsHomeZone(99999));
        }
    }
}
