using Game.Abstractions.Entities;
using Game.Abstractions.Infrastructure;
using Game.Infrastructure.Database;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Game.DataAccess
{
    internal class DataProviderSynchronizer
    {
        private readonly IServiceProvider _services;
        private readonly IPubSubService _pubsub;
        private readonly ICacheService _cache;
        private readonly Task _initSynchronizersTask;

        public DataProviderSynchronizer(IServiceProvider services, IPubSubService pubsub, ICacheService cache)
        {
            _services = services;
            _pubsub = pubsub;
            _cache = cache;
            _initSynchronizersTask = Task.Run(InitSynchronizers);
        }

        public async Task SynchronizeInventory(string sessionKey)
        {
            await _initSynchronizersTask;
            var channel = Constants.PUBSUB_INVENTORY_CHANNEL;
            var queueName = Constants.PUBSUB_INVENTORY_QUEUE;
            await _pubsub.Publish(channel, queueName, sessionKey);
        }

        public async Task SynchronizePlayerData(string sessionKey)
        {
            await _initSynchronizersTask;
            var channel = Constants.PUBSUB_PLAYER_CHANNEL;
            var queueName = Constants.PUBSUB_PLAYER_QUEUE;
            await _pubsub.Publish(channel, queueName, sessionKey);
        }

        public async Task SynchronizeSkills(string sessionKey)
        {
            await _initSynchronizersTask;
            var channel = Constants.PUBSUB_SKILLS_CHANNEL;
            var queueName = Constants.PUBSUB_SKILLS_QUEUE;
            await _pubsub.Publish(channel, queueName, sessionKey);
        }

        private async Task InitSynchronizers()
        {
            await InitInventorySynchronizer();
            await InitPlayerDataSynchronizer();
            await InitSkillsSynchronizer();
        }

        private async Task InitInventorySynchronizer()
        {
            var channel = Constants.PUBSUB_INVENTORY_CHANNEL;
            var queueName = Constants.PUBSUB_INVENTORY_QUEUE;

            var inventoryProcessor = GetPlayerQueueProcessor(async (dbContext, player) =>
            {
#nullable disable
                foreach (var item in player.InventoryItems)
                {
                    item.InventoryItemMods = null;
                }

                player.LogPreferences = null;
                player.PlayerSkills = null;
                player.PlayerAttributes = null;
#nullable enable

                dbContext.Update(player);

                var inventoryItemIds = player.InventoryItems.Select(ii => ii.Id).ToList();
                await dbContext.InventoryItems.Where(ii => ii.PlayerId == player.Id && !inventoryItemIds.Contains(ii.Id)).ExecuteDeleteAsync();

                await dbContext.SaveChangesAsync();
            });

            await _pubsub.Subscribe(channel, queueName, async args => await inventoryProcessor(args.queue));
        }

        private async Task InitPlayerDataSynchronizer()
        {
            var channel = Constants.PUBSUB_PLAYER_CHANNEL;
            var queueName = Constants.PUBSUB_PLAYER_QUEUE;

            var playerProcessor = GetPlayerQueueProcessor(async (dbContext, player) =>
            {
#nullable disable
                player.InventoryItems = null;
                player.LogPreferences = null;
                player.PlayerSkills = null;
                player.PlayerAttributes = null;
#nullable enable

                dbContext.Update(player);
                await dbContext.SaveChangesAsync();
            });

            await _pubsub.Subscribe(channel, queueName, async args => await playerProcessor(args.queue));
        }

        private async Task InitSkillsSynchronizer()
        {
            var channel = Constants.PUBSUB_SKILLS_CHANNEL;
            var queueName = Constants.PUBSUB_SKILLS_QUEUE;

            var skillsProcessor = GetPlayerQueueProcessor((dbContext, player) => throw new NotImplementedException());

            await _pubsub.Subscribe(channel, queueName, async args => await skillsProcessor(args.queue));
        }

        private Func<IPubSubQueue, Task> GetPlayerQueueProcessor(Func<GameContext, Player, Task> action)
        {
            return async (queue) =>
            {
                var nextKey = await queue.GetNextAsync();
                while (nextKey is not null)
                {
                    var player = await _cache.GetAsync<Player>(nextKey);
                    if (player is not null)
                    {
                        var context = _services.GetRequiredService<GameContext>();
                        await action(context, player);
                    }
                    nextKey = await queue.GetNextAsync();
                }
            };
        }
    }
}
