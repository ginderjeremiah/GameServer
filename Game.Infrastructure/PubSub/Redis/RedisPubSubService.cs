using Game.Abstractions.Infrastructure;
using Game.Core;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Collections.Concurrent;

namespace Game.Infrastructure.PubSub.Redis
{
    internal class RedisPubSubService : IPubSubService
    {
        private static readonly ConcurrentDictionary<string, (Action<RedisChannel, RedisValue> handler, BackgroundWorker? worker)> _handles = [];

        private readonly ConnectionMultiplexer _multiplexer;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<RedisPubSubService> _logger;

        public IDatabase Redis => _multiplexer.GetDatabase();
        public ISubscriber Subscriber => _multiplexer.GetSubscriber();

        public RedisPubSubService(ConnectionMultiplexer multiplexer, ILoggerFactory loggerFactory)
        {
            _multiplexer = multiplexer;
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<RedisPubSubService>();
        }

        public async Task Publish(string channel, string message)
        {
            await Redis.PublishAsync(RedisChannel.Literal(channel), message, CommandFlags.FireAndForget);
        }

        public async Task Publish(string channel, string queueName, string? queueData)
        {
            var queue = new RedisQueue(Redis, queueName, _loggerFactory.CreateLogger<RedisQueue>());
            await queue.AddToQueueAsync(queueData);
            await Redis.PublishAsync(RedisChannel.Literal(channel), "");
        }

        public async Task Publish<T>(string channel, string queueName, T queueData)
        {
            var data = queueData?.Serialize();
            await Publish(channel, queueName, data);
        }

        public async Task Subscribe(string channel, Action<(string message, string channel)> action, string? id = null)
        {
            _logger.LogInformation("Creating redis subscriber on channel '{Channel}'.", channel);
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
                action((message.ToString(), channel));
            }
        }

        public async Task Subscribe(string channel, string queueName, Action<(IPubSubQueue queue, string channel)> action, string? id = null)
        {
            _logger.LogInformation("Creating redis subscriber on channel '{Channel}' with queue '{QueueName}'.", channel, queueName);
            var queue = new RedisQueue(Redis, queueName, _loggerFactory.CreateLogger<RedisQueue>());
            var worker = new BackgroundWorker(_loggerFactory.CreateLogger<BackgroundWorker>(), () => action((queue, channel)))
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
            _logger.LogInformation("Creating redis subscriber on channel '{Channel}' with queue '{QueueName}'.", channel, queueName);
            var queue = new RedisQueue(Redis, queueName, _loggerFactory.CreateLogger<RedisQueue>());
            var worker = new BackgroundWorker(_loggerFactory.CreateLogger<BackgroundWorker>(), async () => await action((queue, channel)))
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
