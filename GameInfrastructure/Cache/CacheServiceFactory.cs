using GameCore.Infrastructure;
using GameInfrastructure.Cache.Redis;
using GameInfrastructure.Redis;

namespace GameInfrastructure.Cache
{
    internal static class CacheServiceFactory
    {
        public static ICacheService GetCacheService(ICacheConfiguration config)
        {
            return config.CacheSystem switch
            {
                CacheSystem.Redis => new RedisService(RedisMultiplexerFactory.GetMultiplexer(config)),
                _ => new RedisService(RedisMultiplexerFactory.GetMultiplexer(config))
            };
        }
    }
}
