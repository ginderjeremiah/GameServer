using GameCore;
using GameCore.Infrastructure;
using GameInfrastructure.Cache;
using GameInfrastructure.Database;
using GameInfrastructure.Logging;
using GameInfrastructure.PubSub;

namespace GameInfrastructure
{
    public class DataServicesFactory(IDataServicesConfiguration config) : IDataServicesFactory
    {
        private readonly IDataServicesConfiguration _config = config;
        private IApiLogger? _logger;
        private IDatabaseService? _databaseService;
        private ICacheService? _cache;
        private IPubSubService? _pubsub;

        public IApiLogger Logger => _logger ??= new ApiLogger(_config);
        public IDatabaseService Database => _databaseService ??= GetNewDatabaseService();
        public ICacheService Cache => _cache ??= CacheServiceFactory.GetCacheService(_config);
        public IPubSubService PubSub => _pubsub ??= PubSubServiceFactory.GetPubSubService(_config, Logger);

        public IDatabaseService GetNewDatabaseService()
        {
            return new DatabaseService(_config);
        }
    }
}
