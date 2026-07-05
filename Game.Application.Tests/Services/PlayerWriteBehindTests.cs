using Game.Abstractions.DataAccess;
using Game.Abstractions.Infrastructure;
using Game.Core;
using Game.Core.Events;
using Game.Core.Players;
using Game.DataAccess;
using Game.DataAccess.Repositories;
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

        [Fact]
        public async Task SavePlayer_PublishFailsForANonCancellationReason_ThrowsPlayerPersistenceFlushFailedException()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id);

            var playerRepo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            var player = await playerRepo.GetPlayer(playerEntity.Id);
            Assert.NotNull(player);
            var originalZoneId = player.CurrentZoneId;

            // A repository built with a pubsub that throws for a reason other than cancellation stands in for a
            // transient Redis blip on the flush's *last* attempt in a command's scope (#1632) — SavePlayer must
            // wrap it distinctly rather than let a bare exception propagate, so the socket layer can recognize it
            // and force the connection's in-memory Player to reload afterward.
            var throwingRepo = new PlayerRepository(
                context,
                scope.ServiceProvider.GetRequiredService<ICacheService>(),
                new ThrowingPubSubService(),
                scope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>(),
                scope.ServiceProvider.GetRequiredService<PlayerUpdateBatch>(),
                scope.ServiceProvider.GetRequiredService<IItems>(),
                scope.ServiceProvider.GetRequiredService<IItemMods>(),
                scope.ServiceProvider.GetRequiredService<ISkills>());

            player.ChangeZone(1);
            var ex = await Assert.ThrowsAsync<PlayerPersistenceFlushFailedException>(() => throwingRepo.SavePlayer(player));
            Assert.IsType<InvalidOperationException>(ex.InnerException);

            // The reload's correctness rests on the cache blob never advancing past a failed flush (SavePlayer
            // only writes it once the flush succeeds) — a fresh read must still see the pre-mutation zone, not
            // the ChangeZone(1) this failed save never persisted (#1632).
            var rereadPlayer = await playerRepo.GetPlayer(playerEntity.Id);
            Assert.NotNull(rereadPlayer);
            Assert.Equal(originalZoneId, rereadPlayer.CurrentZoneId);
        }

        private sealed class ThrowingPubSubService : IPubSubService
        {
            public Task Publish(string channel, string message, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Simulated transient publish failure.");
            public Task Publish(string channel, string queueName, string queueData, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Simulated transient publish failure.");
            public Task Publish<T>(string channel, string queueName, T queueData, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Simulated transient publish failure.");
            public Task PublishBatch<T>(string channel, string queueName, IEnumerable<T> queueData, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Simulated transient publish failure.");
            public Task Wake(string channel) => throw new InvalidOperationException("Simulated transient publish failure.");
            public Task Subscribe(string channel, Action<(string message, string channel)> action, string? id = null) => throw new NotImplementedException();
            public Task Subscribe(string channel, string queueName, Func<(IPubSubQueue queue, string channel), Task> action, string id) => throw new NotImplementedException();
            public Task UnSubscribe(string channel, string id) => throw new NotImplementedException();
            public IPubSubQueue GetQueue(string queueName) => throw new NotImplementedException();
        }

        private record SimpleAttributeUpdate(EAttribute Attribute, int Amount) : IAttributeUpdate;
    }
}
