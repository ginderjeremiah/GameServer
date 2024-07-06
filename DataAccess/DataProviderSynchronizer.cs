using GameCore.Entities;
using GameCore.Infrastructure;
using GameInfrastructure;
using GameInfrastructure.Database;

namespace DataAccess
{
    internal class DataProviderSynchronizer
    {
        private static bool _initialized = false;
        private static readonly object _lock = new();
        private readonly IDataServicesFactory _dataServices;

        public DataProviderSynchronizer(IDataServicesFactory dataServices)
        {
            _dataServices = dataServices;
            if (!_initialized)
            {
                lock (_lock)
                {
                    if (!_initialized)
                    {
                        _initialized = true;
                        InitSynchronizers();
                    }
                }
            }
        }

        public async Task SynchronizeInventory(string sessionKey)
        {
            var channel = Constants.PUBSUB_INVENTORY_CHANNEL;
            var queueName = Constants.PUBSUB_INVENTORY_QUEUE;
            await _dataServices.PubSub.Publish(channel, queueName, sessionKey);
        }

        public async Task SynchronizePlayerData(string sessionKey)
        {
            var channel = Constants.PUBSUB_PLAYER_CHANNEL;
            var queueName = Constants.PUBSUB_PLAYER_QUEUE;
            await _dataServices.PubSub.Publish(channel, queueName, sessionKey);
        }

        public async Task SynchronizeSkills(string sessionKey)
        {
            var channel = Constants.PUBSUB_SKILLS_CHANNEL;
            var queueName = Constants.PUBSUB_SKILLS_QUEUE;
            await _dataServices.PubSub.Publish(channel, queueName, sessionKey);
        }

        private void InitSynchronizers()
        {
            InitInventorySynchronizer();
            InitPlayerDataSynchronizer();
            InitSkillsSynchronizer();
        }

        private void InitInventorySynchronizer()
        {
            var channel = Constants.PUBSUB_INVENTORY_CHANNEL;
            var queueName = Constants.PUBSUB_INVENTORY_QUEUE;

            var inventoryProcessor = GetPlayerQueueProcessor(async (database, player) =>
            {
                foreach (var item in player.InventoryItems)
                {
                    item.InventoryItemMods = null;
                }

                player.LogPreferences = null;
                player.PlayerSkills = null;
                player.PlayerAttributes = null;
                database.Update(player);
                await database.SaveChangesAsync();
            });

            _dataServices.PubSub.Subscribe(channel, queueName, async args => await inventoryProcessor(args.queue));
        }

        private void InitPlayerDataSynchronizer()
        {
            var channel = Constants.PUBSUB_PLAYER_CHANNEL;
            var queueName = Constants.PUBSUB_PLAYER_QUEUE;

            var playerProcessor = GetPlayerQueueProcessor(async (database, player) =>
            {
                player.InventoryItems = null;
                player.LogPreferences = null;
                player.PlayerSkills = null;
                player.PlayerAttributes = null;
                database.Update(player);
                await database.SaveChangesAsync();
            });

            _dataServices.PubSub.Subscribe(channel, queueName, async args => await playerProcessor(args.queue));
        }

        private void InitSkillsSynchronizer()
        {
            var channel = Constants.PUBSUB_SKILLS_CHANNEL;
            var queueName = Constants.PUBSUB_SKILLS_QUEUE;

            var skillsProcessor = GetPlayerQueueProcessor((database, player) => throw new NotImplementedException());

            _dataServices.PubSub.Subscribe(channel, queueName, async args => await skillsProcessor(args.queue));
        }

        private Func<IPubSubQueue, Task> GetPlayerQueueProcessor(Func<GameContext, Player, Task> action)
        {
            return async (IPubSubQueue queue) =>
            {
                var nextKey = await queue.GetNextAsync();
                while (nextKey is not null)
                {
                    var player = await _dataServices.Cache.GetAsync<Player>(nextKey);
                    if (player is not null)
                    {
                        await action(_dataServices.GetNewDbContext(), player);
                    }
                    nextKey = await queue.GetNextAsync();
                }
            };
        }
    }
}
