using GameInfrastructure.Cache;
using GameInfrastructure.PubSub;
using StackExchange.Redis;

namespace GameInfrastructure.Redis
{
    internal class RedisMultiplexerFactory
    {
        private static ConnectionMultiplexer? _instance;
        private static readonly object _instanceLock = new();

        private readonly ICacheConfiguration _config;

        public RedisMultiplexerFactory(ICacheConfiguration config)
        {
            _config = config;
        }

        public ConnectionMultiplexer GetMultiplexer()
        {
            if (_instance == null)
            {
                lock (_instanceLock)
                {
                    _instance ??= ConnectionMultiplexer.Connect(_config.CacheConnectionString);
                }
            }
            return _instance;
        }

        public static ConnectionMultiplexer GetMultiplexer(ICacheConfiguration config)
        {
            if (_instance == null)
            {
                lock (_instanceLock)
                {
                    _instance ??= ConnectionMultiplexer.Connect(config.CacheConnectionString);
                }
            }
            return _instance;
        }

        public static ConnectionMultiplexer GetMultiplexer(IPubSubConfiguration config)
        {
            if (_instance == null)
            {
                lock (_instanceLock)
                {
                    _instance ??= ConnectionMultiplexer.Connect(config.PubSubConnectionString);
                }
            }
            return _instance;
        }
    }
}
