using StackExchange.Redis;

namespace DataAccess.Redis
{
    internal class RedisSubscriberWorker
    {
        private readonly AutoResetEvent _resetEvent = new(false);
        private readonly IDataConfiguration _configuration;
        private RepositoryManager? _repositoryManager;

        private RepositoryManager Repositories
        {
            get => _repositoryManager ??= new RepositoryManager(_configuration);
        }

        public RedisSubscriberWorker(IDataConfiguration config, RedisQueue queue, Action<RepositoryManager, RedisValue> processQueueItem)
        {
            _configuration = config;
            Task.Run(() =>
            {
                while (_resetEvent.WaitOne())
                {
                    while (queue.TryGetFromQueue(out var queueValue))
                    {
                        processQueueItem(Repositories, queueValue);
                    }
                }
            });
        }

        public void Start()
        {
            _resetEvent.Set();
        }
    }
}
