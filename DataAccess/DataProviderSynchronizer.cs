using DataAccess.Entities.SessionStore;
using GameCore.PubSub;

namespace DataAccess
{
    internal class DataProviderSynchronizer
    {
        private static bool _initialized = false;
        private static readonly object _lock = new();
        private readonly IPubSubProvider _pubSub;
        private readonly IRepositoryManager _repos;

        public DataProviderSynchronizer(IPubSubProvider pubSubProvider, IRepositoryManager repos)
        {
            _repos = repos;
            _pubSub = pubSubProvider;
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

            var inventoryProcessor = GetSessionQueueProcessor(sessionData =>
                _repos.InventoryItems.UpdateInventoryItemSlots(sessionData.PlayerData.PlayerId, sessionData.InventoryItems)
            );

            _pubSub.Subscribe(channel, queueName, args => inventoryProcessor(args.queue));
        }

        private void InitPlayerDataSynchronizer()
        {
            var channel = Constants.PUBSUB_PLAYER_CHANNEL;
            var queueName = Constants.PUBSUB_PLAYER_QUEUE;

            var playerProcessor = GetSessionQueueProcessor(sessionData => _repos.Players.SavePlayer(sessionData.PlayerData, sessionData.Attributes));

            _pubSub.Subscribe(channel, queueName, args => playerProcessor(args.queue));
        }

        private void InitSkillsSynchronizer()
        {
            var channel = Constants.PUBSUB_SKILLS_CHANNEL;
            var queueName = Constants.PUBSUB_SKILLS_QUEUE;

            var skillsProcessor = GetSessionQueueProcessor(sessionData => throw new NotImplementedException());

            _pubSub.Subscribe(channel, queueName, args => skillsProcessor(args.queue));
        }

        private Action<IPubSubQueue> GetSessionQueueProcessor(Action<SessionData> action)
        {
            return (IPubSubQueue queue) =>
            {
                while (queue.TryGetNext(out var sessionKey))
                {
                    if (_repos.SessionStore.TryGetSession(sessionKey, out var sessionData))
                        action(sessionData);
                }
            };
        }
    }
}
