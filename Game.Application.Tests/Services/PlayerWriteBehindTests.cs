using Game.Abstractions.DataAccess;
using Game.Core;
using Game.Core.Players;
using Game.DataAccess;
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
            Assert.Equal(0, await db.ListLengthAsync(Constants.PUBSUB_PLAYER_QUEUE));

            player.ChangeZone(1);
            await playerRepo.SavePlayer(player);

            // ChangeZone raises exactly one PlayerCoreUpdatedEvent, which must now appear in the queue
            Assert.Equal(1, await db.ListLengthAsync(Constants.PUBSUB_PLAYER_QUEUE));
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

            Assert.Equal(2, await db.ListLengthAsync(Constants.PUBSUB_PLAYER_QUEUE));
        }

        [Fact]
        public async Task SavePlayer_PublishFails_PreservesTheEventForTheNextFlush()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id);

            var playerRepo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            var player = await playerRepo.GetPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var options = ConfigurationOptions.Parse(Containers.PubSubConnectionString);
            using var multiplexer = await ConnectionMultiplexer.ConnectAsync(options);
            var db = multiplexer.GetDatabase();
            Assert.Equal(0, await db.ListLengthAsync(Constants.PUBSUB_PLAYER_QUEUE));

            // A save whose publish fails (a pre-cancelled budget makes the enqueue throw client-side) must not
            // lose the buffered event — PlayerUpdateBatch.FlushAsync only drains once the publish actually
            // succeeds (#1494). The cancellation is client-side only (StackExchange.Redis may still have
            // dispatched the command), so this doesn't assert the queue is untouched at this point — only that
            // nothing is permanently lost by the time the next save flushes.
            player.ChangeZone(1);
            using (var cts = new CancellationTokenSource())
            {
                await cts.CancelAsync();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => playerRepo.SavePlayer(player, cts.Token));
            }

            // The next successful save's flush carries the earlier ChangeZone(1) event (still buffered, since
            // the failed flush never cleared it) alongside this save's own ChangeZone(2) event.
            player.ChangeZone(2);
            await playerRepo.SavePlayer(player);

            Assert.True(await db.ListLengthAsync(Constants.PUBSUB_PLAYER_QUEUE) >= 2);
        }

        private record SimpleAttributeUpdate(EAttribute Attribute, int Amount) : IAttributeUpdate;
    }
}
