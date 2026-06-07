using Game.Abstractions.DataAccess;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Base;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Game.Application.Tests.DataAccess
{
    /// <summary>
    /// Verifies <see cref="IZones.ZoneEnemies"/> returns the spawn read contracts for the requested zone
    /// (and only that zone), exercising the EF projection that backs the reference-data lookup.
    /// </summary>
    [Collection("Integration")]
    public class ZonesIntegrationTests : ApplicationIntegrationTestBase
    {
        public ZonesIntegrationTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

        [Fact]
        public async Task ZoneEnemies_ReturnsTheRequestedZonesSpawnsAsContracts()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var enemy = await TestDataSeeder.CreateEnemyAsync(context, "Spawned");
            var zone = await TestDataSeeder.CreateZoneAsync(context, "Spawn Zone");
            var otherZone = await TestDataSeeder.CreateZoneAsync(context, "Other Zone");
            await TestDataSeeder.LinkEnemyToZoneAsync(context, zone.Id, enemy.Id, weight: 42);

            var zones = scope.ServiceProvider.GetRequiredService<IZones>();

            var result = await zones.ZoneEnemies(zone.Id).ToListAsync(CancellationToken);

            var spawn = Assert.Single(result);
            Assert.Equal(enemy.Id, spawn.EnemyId);
            Assert.Equal(42, spawn.Weight);

            Assert.Empty(await zones.ZoneEnemies(otherZone.Id).ToListAsync(CancellationToken));
        }
    }
}
