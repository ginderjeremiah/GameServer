using Game.Infrastructure.Database;
using Game.TestInfrastructure.Base;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Entities = Game.Infrastructure.Entities;

namespace Game.Application.Tests.DataAccess
{
    /// <summary>
    /// Pins the FK constraints on <see cref="Entities.Player.CurrentZoneId"/>, <see cref="Entities.Player.ClassId"/>,
    /// and <see cref="Entities.UnlockedItem.EquipmentSlotId"/> — the player aggregate's reference columns that
    /// previously carried no DB-level referential integrity (#1823). A dangling reference (a bugged write or a
    /// seed/migration mistake) must fail loudly at the DB rather than silently persisting.
    /// </summary>
    [Collection("Integration")]
    public class PlayerAggregateForeignKeyIntegrationTests : ApplicationIntegrationTestBase
    {
        public PlayerAggregateForeignKeyIntegrationTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

        [Fact]
        public async Task InsertPlayer_DanglingCurrentZoneId_ThrowsDbUpdateException()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context);
            var validClass = await TestDataSeeder.CreateClassAsync(context);

            context.Players.Add(new Entities.Player
            {
                UserId = user.Id,
                ClassId = validClass.Id,
                CurrentZoneId = 12345,
                Name = "Dangling Zone",
                Level = 1,
                Exp = 0,
                StatPointsGained = 0,
                StatPointsUsed = 0,
                LastActivity = DateTime.UtcNow,
            });

            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(CancellationToken));
        }

        [Fact]
        public async Task InsertPlayer_DanglingClassId_ThrowsDbUpdateException()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context);
            var validZone = await TestDataSeeder.CreateZoneAsync(context);

            context.Players.Add(new Entities.Player
            {
                UserId = user.Id,
                ClassId = 12345,
                CurrentZoneId = validZone.Id,
                Name = "Dangling Class",
                Level = 1,
                Exp = 0,
                StatPointsGained = 0,
                StatPointsUsed = 0,
                LastActivity = DateTime.UtcNow,
            });

            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(CancellationToken));
        }

        [Fact]
        public async Task InsertUnlockedItem_DanglingEquipmentSlotId_ThrowsDbUpdateException()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            var item = await TestDataSeeder.CreateItemAsync(context);

            context.UnlockedItems.Add(new Entities.UnlockedItem
            {
                PlayerId = player.Id,
                ItemId = item.Id,
                EquipmentSlotId = 12345,
                Favorite = false,
            });

            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(CancellationToken));
        }
    }
}
