using Game.Core.Infrastructure;
using Game.Infrastructure.Cache.Redis;
using Game.Infrastructure.Redis;
using Microsoft.Extensions.Logging;

namespace Game.Infrastructure.Cache
{
    internal static class CacheServiceFactory
    {
        public static ICacheService GetCacheService(ICacheOptions config, ILoggerFactory loggerFactory)
        {
            return config.CacheSystem switch
            {
                CacheSystem.Redis => CreateRedisService(config, loggerFactory),
                _ => CreateRedisService(config, loggerFactory)
            };
        }

        private static ICacheService CreateRedisService(ICacheOptions config, ILoggerFactory loggerFactory)
        {
            return new RedisService(RedisMultiplexerFactory.GetMultiplexer(config), loggerFactory.CreateLogger<RedisService>());
        }
    }
}
