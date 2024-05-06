using GameCore;
using GameCore.Infrastructure;
using GameInfrastructure.PubSub.Redis;
using GameInfrastructure.Redis;

namespace GameInfrastructure.PubSub
{
    internal static class PubSubServiceFactory
    {
        public static IPubSubService GetPubSubService(IPubSubConfiguration config, IApiLogger logger)
        {
            return config.PubSubSystem switch
            {
                PubSubSystem.Redis => new RedisPubSubService(RedisMultiplexerFactory.GetMultiplexer(config), logger),
                _ => new RedisPubSubService(RedisMultiplexerFactory.GetMultiplexer(config), logger)
            };
        }
    }
}
