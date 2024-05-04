using GameCore.Database.Interfaces;
using StackExchange.Redis;

namespace GameCore.Redis
{
    internal class RedisMultiplexerFactory
    {
        private static ConnectionMultiplexer? _instance;
        private static readonly object _instanceLock = new();

        private readonly IDataConfiguration _configuration;

        public RedisMultiplexerFactory(IDataConfiguration config)
        {
            _configuration = config;
        }

        internal ConnectionMultiplexer GetMultiplexer()
        {
            if (_instance == null)
            {
                lock (_instanceLock)
                {
                    _instance ??= ConnectionMultiplexer.Connect(_configuration.RedisConnectionString);
                }
            }
            return _instance;
        }
    }
}
