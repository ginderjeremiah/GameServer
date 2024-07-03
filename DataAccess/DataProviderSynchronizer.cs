using GameCore.Entities;
using GameCore.Infrastructure;
using GameInfrastructure;
using GameInfrastructure.Database;
using Microsoft.EntityFrameworkCore;

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

        public void SynchronizeInventory(string sessionKey)
        {
            var channel = Constants.PUBSUB_INVENTORY_CHANNEL;
            var queueName = Constants.PUBSUB_INVENTORY_QUEUE;
            _dataServices.PubSub.Publish(channel, queueName, sessionKey);
        }

        public void SynchronizePlayerData(string sessionKey)
        {
            var channel = Constants.PUBSUB_PLAYER_CHANNEL;
            var queueName = Constants.PUBSUB_PLAYER_QUEUE;
            _dataServices.PubSub.Publish(channel, queueName, sessionKey);
        }

        public void SynchronizeSkills(string sessionKey)
        {
            var channel = Constants.PUBSUB_SKILLS_CHANNEL;
            var queueName = Constants.PUBSUB_SKILLS_QUEUE;
            _dataServices.PubSub.Publish(channel, queueName, sessionKey);
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

            var inventoryProcessor = GetSessionQueueProcessor(async (database, sessionData) =>
            {
                var player = database.Players.Include(p => p.InventoryItems).FirstOrDefault(p => p.Id == sessionData.PlayerData.Id);
                if (player is not null)
                {
                    foreach (var item in sessionData.PlayerData.InventoryItems)
                    {
                        item.InventoryItemMods = null;
                    }
                    player.InventoryItems = sessionData.PlayerData.InventoryItems;
                }
                await database.SaveChangesAsync();
            });

            _dataServices.PubSub.Subscribe(channel, queueName, async args => await inventoryProcessor(args.queue));
        }

        private void InitPlayerDataSynchronizer()
        {
            var channel = Constants.PUBSUB_PLAYER_CHANNEL;
            var queueName = Constants.PUBSUB_PLAYER_QUEUE;

            var playerProcessor = GetSessionQueueProcessor(async (database, sessionData) =>
            {
                var player = database.Players.Include(p => p.PlayerAttributes).FirstOrDefault(p => p.Id == sessionData.PlayerData.Id);
                if (player is not null)
                {
                    player.Exp = sessionData.PlayerData.Exp;
                    player.Level = sessionData.PlayerData.Level;
                    player.StatPointsGained = sessionData.PlayerData.StatPointsGained;
                    player.StatPointsUsed = sessionData.PlayerData.StatPointsUsed;
                    player.PlayerAttributes = sessionData.PlayerData.PlayerAttributes;
                }
                await database.SaveChangesAsync();
            });

            _dataServices.PubSub.Subscribe(channel, queueName, async args => await playerProcessor(args.queue));
        }

        private void InitSkillsSynchronizer()
        {
            var channel = Constants.PUBSUB_SKILLS_CHANNEL;
            var queueName = Constants.PUBSUB_SKILLS_QUEUE;

            var skillsProcessor = GetSessionQueueProcessor((database, sessionData) => throw new NotImplementedException());

            _dataServices.PubSub.Subscribe(channel, queueName, async args => await skillsProcessor(args.queue));
        }

        private Func<IPubSubQueue, Task> GetSessionQueueProcessor(Func<GameContext, SessionData, Task> action)
        {
            return async (IPubSubQueue queue) =>
            {
                while (queue.TryGetNext(out var sessionKey))
                {
                    var session = await _dataServices.Cache.GetAsync<SessionData>($"{Constants.CACHE_SESSION_PREFIX}_{sessionKey}");
                    if (session is not null)
                    {
                        await action(_dataServices.GetNewDbContext(), session);
                    }
                }
            };
        }
    }
}
