using Game.Abstractions.DataAccess;
using Game.DataAccess.Repositories;
using Game.Infrastructure.Database;
using Game.Infrastructure.Entities;
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
            await ReloadReferenceCachesAsync();

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
            await ReloadReferenceCachesAsync();

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
            await ReloadReferenceCachesAsync();

            var enemies = scope.ServiceProvider.GetRequiredService<IEnemyEntityCache>();

            Assert.Throws<InvalidOperationException>(() => enemies.GetRandomEnemy(emptyZone.Id));
        }

        [Fact]
        public async Task GetRandomEnemy_ExcludesRetiredEnemiesButKeepsThemResolvable()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var active = await TestDataSeeder.CreateEnemyAsync(context, "Active");
            var retired = await TestDataSeeder.CreateEnemyAsync(context, "Retired");
            var zone = await TestDataSeeder.CreateZoneAsync(context);
            await TestDataSeeder.LinkEnemyToZoneAsync(context, zone.Id, active.Id, weight: 1);
            await TestDataSeeder.LinkEnemyToZoneAsync(context, zone.Id, retired.Id, weight: 1);

            // Retire one of the two zone enemies: out of the spawn rolls, but kept at its slot.
            retired.RetiredAt = DateTime.UtcNow;
            await context.SaveChangesAsync(CancellationToken);
            await ReloadReferenceCachesAsync();

            var enemies = scope.ServiceProvider.GetRequiredService<IEnemyEntityCache>();

            // Every random draw avoids the retired enemy...
            for (int i = 0; i < 100; i++)
            {
                Assert.Equal(active.Id, enemies.GetRandomEnemy(zone.Id).Id);
            }

            // ...but the retired enemy still resolves by id, so existing references stay valid.
            Assert.Equal(retired.Id, enemies.GetEnemy(retired.Id)?.Id);
        }

        [Fact]
        public async Task GetEnemy_RetiringNonTerminalEnemy_DoesNotMisResolveHigherIds()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            // Contiguous zero-based ids 0..4 (truncation's RESTART IDENTITY seeds the next id at 0).
            var seeded = new List<Enemy>();
            for (int i = 0; i < 5; i++)
            {
                seeded.Add(await TestDataSeeder.CreateEnemyAsync(context, $"Enemy {i}"));
            }

            // Retire a NON-terminal record. A hard delete here would open an id gap and shift every
            // higher index down by one; retiring keeps the slot so index-based lookups stay correct.
            var target = seeded[2];
            target.RetiredAt = DateTime.UtcNow;
            await context.SaveChangesAsync(CancellationToken);
            await ReloadReferenceCachesAsync();

            var enemies = scope.ServiceProvider.GetRequiredService<IEnemyEntityCache>();

            // The retired record still resolves to itself (not null, not a neighbour).
            Assert.Equal(target.Id, enemies.GetEnemy(target.Id)?.Id);

            // Every higher id resolves to its own record — no off-by-one mis-resolution.
            foreach (var enemy in seeded.Where(e => e.Id > target.Id))
            {
                Assert.Equal(enemy.Id, enemies.GetEnemy(enemy.Id)?.Id);
            }
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
            await ReloadReferenceCachesAsync();

            var enemies = scope.ServiceProvider.GetRequiredService<IEnemies>();

            var domainEnemy = enemies.GetRandomDomainEnemy(zone.Id, level: 7);

            Assert.Equal(enemy.Id, domainEnemy.Id);
            Assert.Equal(7, domainEnemy.Level);
            Assert.Contains(domainEnemy.AvailableSkills, s => s.Id == skill.Id);
        }
    }
}
