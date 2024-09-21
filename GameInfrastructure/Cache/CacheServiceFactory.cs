using GameCore;
using GameCore.Infrastructure;
using GameInfrastructure.Cache.Redis;
using GameInfrastructure.Redis;

namespace GameInfrastructure.Cache
{
    internal static class CacheServiceFactory
    {
        public static ICacheService GetCacheService(ICacheConfiguration config, IApiLogger logger)
        {
            return config.CacheSystem switch
            {
                CacheSystem.Redis => new RedisService(RedisMultiplexerFactory.GetMultiplexer(config), logger),
                _ => new RedisService(RedisMultiplexerFactory.GetMultiplexer(config), logger)
            };
        }
    }
}
