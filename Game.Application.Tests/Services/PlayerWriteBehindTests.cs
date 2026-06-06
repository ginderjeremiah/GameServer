using Game.Abstractions.DataAccess;
using Game.Core;
using Game.Core.Players;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Base;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Xunit;

namespace Game.Application.Tests.Services
{
    [Collection("Integration")]
    public class PlayerWriteBehindTests : ApplicationIntegrationTestBase
    {
        public PlayerWriteBehindTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

        [Fact]
        public async Task SavePlayer_RaisesPersistenceEvent_PublishesToQueue()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id);

            var playerRepo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            var player = await playerRepo.GetPlayer(playerEntity.Id);
            Assert.NotNull(player);

            // Verify the queue starts empty
            var options = ConfigurationOptions.Parse(Containers.PubSubConnectionString);
            using var multiplexer = await ConnectionMultiplexer.ConnectAsync(options);
            var db = multiplexer.GetDatabase();
            Assert.Equal(0, await db.ListLengthAsync("PlayerUpdateQueue"));

            player.ChangeZone(1);
            await playerRepo.SavePlayer(player);

            // ChangeZone raises PlayerCoreUpdatedEvent, which must now appear in the queue
            Assert.True(await db.ListLengthAsync("PlayerUpdateQueue") > 0);
        }

        [Fact]
        public async Task SavePlayer_MultipleEvents_PublishesAllToQueue()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id);

            // Give the player available stat points to spend
            playerEntity.StatPointsGained = 106;
            await context.SaveChangesAsync(CancellationToken);

            var playerRepo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            var player = await playerRepo.GetPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var options = ConfigurationOptions.Parse(Containers.PubSubConnectionString);
            using var multiplexer = await ConnectionMultiplexer.ConnectAsync(options);
            var db = multiplexer.GetDatabase();

            // TryUpdateAttributes raises PlayerCoreUpdatedEvent + AttributeAllocationsChangedEvent
            var updated = player.TryUpdateAttributes([new SimpleAttributeUpdate(EAttribute.Strength, 1)]);
            Assert.True(updated);
            await playerRepo.SavePlayer(player);

            Assert.Equal(2, await db.ListLengthAsync("PlayerUpdateQueue"));
        }

        private record SimpleAttributeUpdate(EAttribute Attribute, int Amount) : IAttributeUpdate;
    }
}
