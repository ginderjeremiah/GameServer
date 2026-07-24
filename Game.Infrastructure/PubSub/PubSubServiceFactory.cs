using Game.Abstractions.Infrastructure;
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
                // Fail loud on an unsupported value rather than silently defaulting to Redis (#453).
                _ => throw new InvalidOperationException(
                    $"Unsupported PubSubSystem '{config.PubSubSystem}'. The application only supports "
                    + $"{nameof(PubSubSystem.Redis)}; an unrecognized value is rejected rather than defaulted.")
            };
        }

        private static IPubSubService CreateRedisPubSubService(IPubSubOptions config, ILoggerFactory loggerFactory)
        {
            var multiplexer = RedisMultiplexerFactory.GetMultiplexer(config, loggerFactory.CreateLogger(nameof(RedisMultiplexerFactory)));
            return new RedisPubSubService(multiplexer, loggerFactory);
        }
    }
}
