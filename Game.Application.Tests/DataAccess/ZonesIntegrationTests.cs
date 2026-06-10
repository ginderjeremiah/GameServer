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
    /// Verifies the <see cref="IZones"/> read paths: <see cref="IZones.GetZone"/> projects a zone to its
    /// read contract, and the two not-found behaviours (<see cref="IZones.GetZone"/> throws on a bad id;
    /// <see cref="IZoneEntityCache.LookupZone"/> returns <c>null</c>).
    /// </summary>
    [Collection("Integration")]
    public class ZonesIntegrationTests : ApplicationIntegrationTestBase
    {
        public ZonesIntegrationTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

        [Fact]
        public async Task GetZone_ReturnsZoneAsContract()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var zone = await TestDataSeeder.CreateZoneAsync(context, "Read Zone", levelMin: 3, levelMax: 9, order: 2);

            var zones = scope.ServiceProvider.GetRequiredService<IZones>();

            var result = zones.GetZone(zone.Id);

            Assert.Equal(zone.Id, result.Id);
            Assert.Equal("Read Zone", result.Name);
            Assert.Equal(3, result.LevelMin);
            Assert.Equal(9, result.LevelMax);
            Assert.Equal(2, result.Order);
        }

        [Fact]
        public async Task GetZone_WithDedicatedBoss_ReturnsBossFields()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var boss = await TestDataSeeder.CreateEnemyAsync(context, "Catacomb Lich", isBoss: true);
            var zone = await TestDataSeeder.CreateZoneAsync(
                context, "Forgotten Catacombs", levelMin: 8, levelMax: 11, bossEnemyId: boss.Id, bossLevel: 18);

            var zones = scope.ServiceProvider.GetRequiredService<IZones>();

            var result = zones.GetZone(zone.Id);

            Assert.Equal(boss.Id, result.BossEnemyId);
            Assert.Equal(18, result.BossLevel);
        }

        [Fact]
        public async Task GetZone_WithoutBoss_ReturnsNullBossEnemyId()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var zone = await TestDataSeeder.CreateZoneAsync(context, "Bossless Glade");

            var zones = scope.ServiceProvider.GetRequiredService<IZones>();

            var result = zones.GetZone(zone.Id);

            Assert.Null(result.BossEnemyId);
        }

        [Fact]
        public void GetZone_InvalidId_Throws()
        {
            using var scope = CreateScope();
            var zones = scope.ServiceProvider.GetRequiredService<IZones>();

            Assert.Throws<ArgumentOutOfRangeException>(() => zones.GetZone(99999));
        }

        [Fact]
        public async Task GetDomainZone_ReturnsZoneAsDomainModelWithBossFields()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var boss = await TestDataSeeder.CreateEnemyAsync(context, "Catacomb Lich", isBoss: true);
            var zone = await TestDataSeeder.CreateZoneAsync(
                context, "Forgotten Catacombs", levelMin: 8, levelMax: 11, bossEnemyId: boss.Id, bossLevel: 18);

            var zones = scope.ServiceProvider.GetRequiredService<IZones>();

            var result = zones.GetDomainZone(zone.Id);

            Assert.Equal(zone.Id, result.Id);
            Assert.Equal(8, result.LevelMin);
            Assert.Equal(11, result.LevelMax);
            Assert.Equal(boss.Id, result.BossEnemyId);
            Assert.Equal(18, result.BossLevel);
            Assert.True(result.HasBoss);
        }

        [Fact]
        public async Task GetDomainZone_WithoutBoss_HasNoBoss()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var zone = await TestDataSeeder.CreateZoneAsync(context, "Bossless Glade");

            var zones = scope.ServiceProvider.GetRequiredService<IZones>();

            var result = zones.GetDomainZone(zone.Id);

            Assert.Null(result.BossEnemyId);
            Assert.False(result.HasBoss);
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

    }
}
