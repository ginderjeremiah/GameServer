using GameCore.Cache.Redis;
using GameCore.Redis;
using Microsoft.Extensions.DependencyInjection;

namespace GameCore.Cache
{
    internal static class CacheProviderFactory
    {
        public static void AddCacheProviderService(IServiceCollection services)
        {
            services.AddTransient<RedisMultiplexerFactory>();
            services.AddTransient<ICacheProvider, RedisProvider>();
        }
    }
}
