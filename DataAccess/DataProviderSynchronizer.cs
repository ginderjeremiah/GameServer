using GameCore;
using GameCore.Entities;
using GameCore.Infrastructure;

namespace DataAccess
{
    internal class DataProviderSynchronizer
    {
        private static bool _initialized = false;
        private static readonly object _lock = new();
        private readonly IPubSubService _pubSub;
        private readonly IRepositoryManager _repos;

        public DataProviderSynchronizer(IPubSubService pubSub, IRepositoryManager repos)
        {
            _repos = repos;
            _pubSub = pubSub;
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
            _pubSub.Publish(channel, queueName, sessionKey);
        }

        public void SynchronizePlayerData(string sessionKey)
        {
            var channel = Constants.PUBSUB_PLAYER_CHANNEL;
            var queueName = Constants.PUBSUB_PLAYER_QUEUE;
            _pubSub.Publish(channel, queueName, sessionKey);
        }

        public void SynchronizeSkills(string sessionKey)
        {
            var channel = Constants.PUBSUB_SKILLS_CHANNEL;
            var queueName = Constants.PUBSUB_SKILLS_QUEUE;
            _pubSub.Publish(channel, queueName, sessionKey);
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

            var inventoryProcessor = GetSessionQueueProcessor(async sessionData =>
                await _repos.InventoryItems.UpdateInventoryItemSlotsAsync(sessionData.PlayerData.Id, sessionData.InventoryItems)
            );

            _pubSub.Subscribe(channel, queueName, args => inventoryProcessor(args.queue));
        }

        private void InitPlayerDataSynchronizer()
        {
            var channel = Constants.PUBSUB_PLAYER_CHANNEL;
            var queueName = Constants.PUBSUB_PLAYER_QUEUE;

            var playerProcessor = GetSessionQueueProcessor(sessionData => _repos.Players.SavePlayerAsync(sessionData.PlayerData, sessionData.Attributes));

            _pubSub.Subscribe(channel, queueName, args => playerProcessor(args.queue));
        }

        private void InitSkillsSynchronizer()
        {
            var channel = Constants.PUBSUB_SKILLS_CHANNEL;
            var queueName = Constants.PUBSUB_SKILLS_QUEUE;

            var skillsProcessor = GetSessionQueueProcessor(sessionData => throw new NotImplementedException());

            _pubSub.Subscribe(channel, queueName, args => skillsProcessor(args.queue));
        }

        private Action<IPubSubQueue> GetSessionQueueProcessor(Func<SessionData, Task> action)
        {
            return async (IPubSubQueue queue) =>
            {
                while (queue.TryGetNext(out var sessionKey))
                {
                    var session = await _repos.SessionStore.GetSessionAsync(sessionKey);
                    if (session is not null)
                    {
                        await action(session);
                    }
                }
            };
        }
    }
}
