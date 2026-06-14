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
        public async Task SavePlayer_WhenSourceOfTruthCacheWriteFails_SurfacesTheFailure()
        {
            using var scope = CreateScope();
            var sp = scope.ServiceProvider;
            var context = sp.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id);

            // Wrap the real cache so the awaited source-of-truth write fails while reads/load still work,
            // standing in for a dropped Redis write. The fix awaits that write, so the failure must propagate
            // rather than being swallowed by the old fire-and-forget SetAndForget (#580).
            var throwingCache = new ThrowingOnSetCacheService(sp.GetRequiredService<ICacheService>());
            var repo = new PlayerRepository(
                context,
                throwingCache,
                sp.GetRequiredService<IPubSubService>(),
                sp.GetRequiredService<IDomainEventDispatcher>(),
                sp.GetRequiredService<PlayerUpdateBatch>(),
                sp.GetRequiredService<IItems>(),
                sp.GetRequiredService<IItemMods>(),
                sp.GetRequiredService<ISkills>());

            var player = await repo.GetPlayer(playerEntity.Id);
            Assert.NotNull(player);

            player.ChangeZone(1);

            await Assert.ThrowsAsync<CacheWriteFailedException>(() => repo.SavePlayer(player));
        }

        private record SimpleAttributeUpdate(EAttribute Attribute, int Amount) : IAttributeUpdate;
    }
}
