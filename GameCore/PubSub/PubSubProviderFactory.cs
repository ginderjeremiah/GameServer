using GameCore.PubSub.Redis;
using GameCore.Redis;
using Microsoft.Extensions.DependencyInjection;

namespace GameCore.PubSub
{
    internal class PubSubProviderFactory
    {
        public static void AddPubSubProviderService(IServiceCollection services)
        {
            services.AddTransient<RedisMultiplexerFactory>();
            services.AddTransient<IPubSubProvider, RedisPubSubProvider>();
        }
    }
}
