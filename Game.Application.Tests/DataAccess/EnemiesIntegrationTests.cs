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
    [Collection("Integration")]
    public class EnemiesIntegrationTests : ApplicationIntegrationTestBase
    {
        public EnemiesIntegrationTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

        [Fact]
        public async Task GetRandomEnemy_OnlyReturnsEnemiesAssignedToTheRequestedZone()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var enemyA1 = await TestDataSeeder.CreateEnemyAsync(context, "Zone A Enemy 1");
            var enemyA2 = await TestDataSeeder.CreateEnemyAsync(context, "Zone A Enemy 2");
            var enemyB = await TestDataSeeder.CreateEnemyAsync(context, "Zone B Enemy");

            var zoneA = await TestDataSeeder.CreateZoneAsync(context, "Zone A");
            var zoneB = await TestDataSeeder.CreateZoneAsync(context, "Zone B");

            await TestDataSeeder.LinkEnemyToZoneAsync(context, zoneA.Id, enemyA1.Id, weight: 1);
            await TestDataSeeder.LinkEnemyToZoneAsync(context, zoneA.Id, enemyA2.Id, weight: 3);
            await TestDataSeeder.LinkEnemyToZoneAsync(context, zoneB.Id, enemyB.Id, weight: 1);

            var enemies = scope.ServiceProvider.GetRequiredService<IEnemyEntityCache>();

            var zoneAEnemyIds = new HashSet<int> { enemyA1.Id, enemyA2.Id };

            // Draw many times; every draw must belong to the requested zone.
            for (int i = 0; i < 100; i++)
            {
                Assert.Contains(enemies.GetRandomEnemy(zoneA.Id).Id, zoneAEnemyIds);
                Assert.Equal(enemyB.Id, enemies.GetRandomEnemy(zoneB.Id).Id);
            }
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(1)]
        [InlineData(999)]
        public async Task GetRandomEnemy_InvalidZoneId_ThrowsArgumentOutOfRange(int invalidZoneId)
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            // A single valid zone (Id 0) means every other id is out of range, including a large
            // one that previously grew the spawn-table list unbounded instead of being rejected.
            var enemy = await TestDataSeeder.CreateEnemyAsync(context);
            var zone = await TestDataSeeder.CreateZoneAsync(context);
            await TestDataSeeder.LinkEnemyToZoneAsync(context, zone.Id, enemy.Id);

            var enemies = scope.ServiceProvider.GetRequiredService<IEnemyEntityCache>();

            Assert.Throws<ArgumentOutOfRangeException>(() => enemies.GetRandomEnemy(invalidZoneId));
        }

        [Fact]
        public async Task GetRandomEnemy_ValidZoneWithNoEnemies_ThrowsInvalidOperation()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var enemy = await TestDataSeeder.CreateEnemyAsync(context);
            var populatedZone = await TestDataSeeder.CreateZoneAsync(context, "Populated");
            var emptyZone = await TestDataSeeder.CreateZoneAsync(context, "Empty");
            await TestDataSeeder.LinkEnemyToZoneAsync(context, populatedZone.Id, enemy.Id);

            var enemies = scope.ServiceProvider.GetRequiredService<IEnemyEntityCache>();

            Assert.Throws<InvalidOperationException>(() => enemies.GetRandomEnemy(emptyZone.Id));
        }

        [Fact]
        public async Task GetRandomDomainEnemy_ValidZone_MapsEntityAtRequestedLevel()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var skill = await TestDataSeeder.CreateSkillAsync(context);
            var enemy = await TestDataSeeder.CreateEnemyAsync(context);
            await TestDataSeeder.LinkSkillToEnemyAsync(context, enemy.Id, skill.Id);
            var zone = await TestDataSeeder.CreateZoneAsync(context);
            await TestDataSeeder.LinkEnemyToZoneAsync(context, zone.Id, enemy.Id);

            var enemies = scope.ServiceProvider.GetRequiredService<IEnemies>();

            var domainEnemy = enemies.GetRandomDomainEnemy(zone.Id, level: 7);

            Assert.Equal(enemy.Id, domainEnemy.Id);
            Assert.Equal(7, domainEnemy.Level);
            Assert.Contains(domainEnemy.AvailableSkills, s => s.Id == skill.Id);
        }
    }
}
