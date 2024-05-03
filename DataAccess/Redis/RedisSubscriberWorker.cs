using GameLibrary.Database.Interfaces;
using GameLibrary.Logging;
using StackExchange.Redis;

namespace DataAccess.Redis
{
    internal class RedisSubscriberWorker
    {
        private readonly AutoResetEvent _resetEvent = new(false);
        private readonly IDataConfiguration _configuration;
        private readonly IApiLogger _logger;
        private readonly IDataProvider _database;
        private RepositoryManager? _repositoryManager;

        private RepositoryManager Repositories
        {
            get => _repositoryManager ??= new RepositoryManager(_configuration, _logger, _database);
        }

        public RedisSubscriberWorker(IDataConfiguration config, RedisQueue queue, Action<RepositoryManager, RedisValue> processQueueItem, IApiLogger logger, IDataProvider database)
        {
            _configuration = config;
            _logger = logger;
            _database = database;
            Task.Run(() =>
            {
                while (_resetEvent.WaitOne())
                {
                    while (queue.TryGetFromQueue(out var queueValue))
                    {
                        try
                        {
                            processQueueItem(Repositories, queueValue);
                        }
                        catch (Exception ex)
                        {
                            _logger.Log(ex);
                        }
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
