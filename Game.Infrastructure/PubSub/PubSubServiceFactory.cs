using Game.Core.Infrastructure;
using Game.Infrastructure.PubSub.Redis;
using Game.Infrastructure.Redis;
using Microsoft.Extensions.Logging;

namespace Game.Infrastructure.PubSub
{
    internal static class PubSubServiceFactory
    {
        public static IPubSubService GetPubSubService(IPubSubOptions config, ILoggerFactory loggerFactory)
        {
            return config.PubSubSystem switch
            {
                PubSubSystem.Redis => CreateRedisPubSubService(config, loggerFactory),
                _ => CreateRedisPubSubService(config, loggerFactory)
            };
        }

        private static IPubSubService CreateRedisPubSubService(IPubSubOptions config, ILoggerFactory loggerFactory)
        {
            return new RedisPubSubService(RedisMultiplexerFactory.GetMultiplexer(config), loggerFactory);
        }
    }
}
