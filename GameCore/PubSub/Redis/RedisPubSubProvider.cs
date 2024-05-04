using GameCore.Logging.Interfaces;
using GameCore.Redis;
using StackExchange.Redis;

namespace GameCore.PubSub.Redis
{
    internal class RedisPubSubProvider : IPubSubProvider
    {
        private ConnectionMultiplexer Multiplexer { get; }
        private IApiLogger Logger { get; }

        public IDatabase Redis => Multiplexer.GetDatabase();
        public ISubscriber Subscriber => Multiplexer.GetSubscriber();

        public RedisPubSubProvider(RedisMultiplexerFactory factory, IApiLogger logger)
        {
            Multiplexer = factory.GetMultiplexer();
            Logger = logger;
        }

        public void Publish(string channel, string message)
        {
            Redis.Publish(RedisChannel.Literal(channel), message, CommandFlags.FireAndForget);
        }

        public void Publish(string channel, string queueName, string? queueData)
        {
            var queue = new RedisQueue(Redis, queueName);
            queue.AddToQueue(queueData);
            Redis.Publish(RedisChannel.Literal(channel), "");
        }

        public void Publish<T>(string channel, string queueName, T queueData)
        {
            var data = queueData.Serialize();
            Publish(channel, queueName, data);
        }

        public void Subscribe(string channel, Action<(string message, string channel)> action)
        {
            Logger.LogInfo($"Creating redis subscriber on channel '{channel}'.");
            Subscriber.Subscribe(RedisChannel.Literal(channel), (_, message) =>
            {
                action((message.AsString(), channel));
            });
        }

        public void Subscribe(string channel, string queueName, Action<(IPubSubQueue queue, string channel)> action)
        {
            Logger.LogInfo($"Creating redis subscriber on channel '{channel}' with queue '{queueName}'.");
            var queue = new RedisQueue(Redis, queueName);
            var worker = new BackgroundWorker(Logger, () => action((queue, channel)));
            worker.Name = $"RedisSubWorker_{queueName}";
            Subscriber.Subscribe(RedisChannel.Literal(channel), (_, _) => worker.Start());
        }
    }
}
