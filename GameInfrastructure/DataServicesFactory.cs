using GameCore;
using GameCore.Infrastructure;
using GameInfrastructure.Cache;
using GameInfrastructure.Database;
using GameInfrastructure.Logging;
using GameInfrastructure.PubSub;

namespace GameInfrastructure
{
    public class DataServicesFactory : IDataServicesFactory
    {
        private readonly IDataServicesConfiguration _config;
        private IApiLogger? _logger;
        private IDatabaseService? _database;
        private ICacheService? _cache;
        private IPubSubService? _pubsub;

        public IApiLogger Logger => _logger ??= new ApiLogger(_config);
        public IDatabaseService Database => _database ??= DatabaseServiceFactory.GetDatabaseService(_config);
        public ICacheService Cache => _cache ??= CacheServiceFactory.GetCacheService(_config);
        public IPubSubService PubSub => _pubsub ??= PubSubServiceFactory.GetPubSubService(_config, Logger);

        public DataServicesFactory(IDataServicesConfiguration config)
        {
            _config = config;
        }
    }
}
