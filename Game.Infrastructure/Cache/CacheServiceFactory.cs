using Game.Abstractions.Infrastructure;
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
                // Fail loud on an unsupported value rather than silently defaulting to Redis (#453).
                _ => throw new InvalidOperationException(
                    $"Unsupported CacheSystem '{config.CacheSystem}'. The application only supports "
                    + $"{nameof(CacheSystem.Redis)}; an unrecognized value is rejected rather than defaulted.")
            };
        }

        private static ICacheService CreateRedisService(ICacheOptions config, ILoggerFactory loggerFactory)
        {
            return new RedisService(RedisMultiplexerFactory.GetMultiplexer(config), loggerFactory.CreateLogger<RedisService>());
        }
    }
}
