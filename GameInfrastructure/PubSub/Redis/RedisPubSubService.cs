using GameCore;
using GameCore.Infrastructure;
using StackExchange.Redis;
using System.Collections.Concurrent;

namespace GameInfrastructure.PubSub.Redis
{
    internal class RedisPubSubService : IPubSubService
    {
        private static readonly ConcurrentDictionary<string, (Action<RedisChannel, RedisValue> handler, BackgroundWorker? worker)> _handles = [];

        private ConnectionMultiplexer Multiplexer { get; }
        private IApiLogger Logger { get; }

        public IDatabase Redis => Multiplexer.GetDatabase();
        public ISubscriber Subscriber => Multiplexer.GetSubscriber();

        public RedisPubSubService(ConnectionMultiplexer multiplexer, IApiLogger logger)
        {
            Multiplexer = multiplexer;
            Logger = logger;
        }

        public async Task Publish(string channel, string message)
        {
            await Redis.PublishAsync(RedisChannel.Literal(channel), message, CommandFlags.FireAndForget);
        }

        public async Task Publish(string channel, string queueName, string queueData)
        {
            var queue = new RedisQueue(Redis, queueName, Logger);
            await queue.AddToQueueAsync(queueData);
            await Redis.PublishAsync(RedisChannel.Literal(channel), "");
        }

        public async Task Publish<T>(string channel, string queueName, T queueData)
        {
            var data = queueData.Serialize();
            await Publish(channel, queueName, data);
        }

        public async Task Subscribe(string channel, Action<(string message, string channel)> action, string? id = null)
        {
            Logger.LogInfo($"Creating redis subscriber on channel '{channel}'.");
            if (id is not null)
            {
                if (!_handles.TryAdd(id, (handle, null)))
                {
                    throw new InvalidOperationException($"Cannot create handle with id: {id} because one already exists.");
                }
            }

            await Subscriber.SubscribeAsync(RedisChannel.Literal(channel), handle);

            void handle(RedisChannel _, RedisValue message)
            {
                action((message.AsString(), channel));
            }
        }

        public async Task Subscribe(string channel, string queueName, Action<(IPubSubQueue queue, string channel)> action, string? id = null)
        {
            Logger.LogInfo($"Creating redis subscriber on channel '{channel}' with queue '{queueName}'.");
            var queue = new RedisQueue(Redis, queueName, Logger);
            var worker = new BackgroundWorker(Logger, () => action((queue, channel)))
            {
                Name = $"RedisSubWorker_{queueName}"
            };

            if (id is not null)
            {
                if (!_handles.TryAdd(id, (handle, worker)))
                {
                    worker.Kill();
                    throw new InvalidOperationException($"Cannot create handle with id: {id} because one already exists.");
                }
            }

            await Subscriber.SubscribeAsync(RedisChannel.Literal(channel), handle);

            void handle(RedisChannel _c, RedisValue _v)
            {
                worker.Start();
            }
        }

        public async Task Subscribe(string channel, string queueName, Func<(IPubSubQueue queue, string channel), Task> action, string? id = null)
        {
            Logger.LogInfo($"Creating redis subscriber on channel '{channel}' with queue '{queueName}'.");
            var queue = new RedisQueue(Redis, queueName, Logger);
            var worker = new BackgroundWorker(Logger, async () => await action((queue, channel)))
            {
                Name = $"RedisSubWorker_{queueName}"
            };

            if (id is not null)
            {
                if (!_handles.TryAdd(id, (handle, worker)))
                {
                    worker.Kill();
                    throw new InvalidOperationException($"Cannot create handle with id: {id} because one already exists.");
                }
            }

            await Subscriber.SubscribeAsync(RedisChannel.Literal(channel), handle);

            void handle(RedisChannel _c, RedisValue _v)
            {
                worker.Start();
            }
        }

        public async Task UnSubscribe(string channel)
        {
            await Subscriber.UnsubscribeAsync(RedisChannel.Literal(channel));
        }

        public async Task UnSubscribe(string channel, string id)
        {
            if (_handles.TryRemove(id, out var handle))
            {
                handle.worker?.Kill();
                await Subscriber.UnsubscribeAsync(RedisChannel.Literal(channel), handle.handler);
            }
        }
    }
}
