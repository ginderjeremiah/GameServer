using GameInfrastructure.Cache;
using GameInfrastructure.PubSub;
using StackExchange.Redis;

namespace GameInfrastructure.Redis
{
    internal class RedisMultiplexerFactory
    {
        private static ConnectionMultiplexer? _cacheInstance;
        private static ConnectionMultiplexer? _pubsubInstance;
        private static readonly object _cacheLock = new();
        private static readonly object _pubsubLock = new();

        private readonly ICacheConfiguration _config;

        public RedisMultiplexerFactory(ICacheConfiguration config)
        {
            _config = config;
        }

        public ConnectionMultiplexer GetMultiplexer()
        {
            if (_cacheInstance is not null)
            {
                return _cacheInstance;
            }
            else if (_pubsubInstance is not null)
            {
                return _pubsubInstance;
            }
            else
            {
                lock (_cacheLock)
                {
                    _cacheInstance ??= ConnectionMultiplexer.Connect(_config.CacheConnectionString);
                }

                return _cacheInstance;
            }
        }

        public static ConnectionMultiplexer GetMultiplexer(ICacheConfiguration config)
        {
            if (_cacheInstance is null)
            {
                lock (_cacheLock)
                {
                    if (_pubsubInstance is not null && _pubsubInstance.Configuration == config.CacheConnectionString)
                    {
                        _cacheInstance = _pubsubInstance;
                    }
                    else
                    {
                        _cacheInstance ??= ConnectionMultiplexer.Connect(config.CacheConnectionString);
                    }
                }
            }

            return _cacheInstance;
        }

        public static ConnectionMultiplexer GetMultiplexer(IPubSubConfiguration config)
        {
            if (_pubsubInstance is null)
            {
                lock (_cacheLock)
                {
                    if (_cacheInstance is not null && _cacheInstance.Configuration == config.PubSubConnectionString)
                    {
                        _pubsubInstance = _cacheInstance;
                    }
                    else
                    {
                        _pubsubInstance ??= ConnectionMultiplexer.Connect(config.PubSubConnectionString);
                    }
                }
            }

            return _pubsubInstance;
        }
    }
}
