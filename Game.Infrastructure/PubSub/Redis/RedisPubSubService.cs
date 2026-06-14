using Game.Abstractions.Infrastructure;
using Game.Core;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Collections.Concurrent;

namespace Game.Infrastructure.PubSub.Redis
{
    // Not IDisposable on purpose: the worker registry below is static (process-wide) while this service is
    // registered transient, so disposing one instance would tear down workers owned by every other instance.
    // Each worker is instead disposed at its own teardown point — when its subscription is removed (UnSubscribe)
    // or when it is discarded because its id was already taken — which is the correct ownership boundary.
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

        public IPubSubQueue GetQueue(string queueName)
        {
            return new RedisQueue(Redis, queueName, _loggerFactory.CreateLogger<RedisQueue>());
        }

        public async Task Publish(string channel, string queueName, string queueData)
        {
            var queue = GetQueue(queueName);
            // The queue write is the durable part and stays awaited. The channel publish is only a wake
            // signal for the queue consumer, and Redis pub/sub is already at-most-once (awaiting it confirms
            // the command was sent, not that any subscriber received it), so it is fire-and-forget: the data
            // is safely enqueued regardless, and the consumer drains the whole queue on its next wake (#552).
            await queue.AddToQueueAsync(queueData);
            await Redis.PublishAsync(RedisChannel.Literal(channel), "", CommandFlags.FireAndForget);
        }

        public async Task Publish<T>(string channel, string queueName, T queueData)
        {
            await Publish(channel, queueName, queueData.Serialize());
        }

        public async Task PublishBatch<T>(string channel, string queueName, IEnumerable<T> queueData)
        {
            var values = queueData.Select(data => data.Serialize()).ToArray();
            if (values.Length == 0)
            {
                return;
            }

            // One multi-value LPUSH carries the whole batch durably; the single wake publish is fire-and-forget
            // for the same reason as the per-event Publish above — the data is already enqueued and the consumer
            // drains the whole queue on its next wake (#559).
            var queue = GetQueue(queueName);
            await queue.AddRangeToQueueAsync(values);
            await Redis.PublishAsync(RedisChannel.Literal(channel), "", CommandFlags.FireAndForget);
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
            var queue = GetQueue(queueName);
            var worker = new BackgroundWorker(_loggerFactory.CreateLogger<BackgroundWorker>(), () => action((queue, channel)))
            {
                Name = $"RedisSubWorker_{queueName}"
            };

            await SubscribeWithWorker(channel, worker, id);
        }

        public async Task Subscribe(string channel, string queueName, Func<(IPubSubQueue queue, string channel), Task> action, string? id = null)
        {
            _logger.LogInformation("Creating redis subscriber on channel '{Channel}' with queue '{QueueName}'.", channel, queueName);
            var queue = GetQueue(queueName);
            var worker = new BackgroundWorker(_loggerFactory.CreateLogger<BackgroundWorker>(), async () => await action((queue, channel)))
            {
                Name = $"RedisSubWorker_{queueName}"
            };

            await SubscribeWithWorker(channel, worker, id);
        }

        // Registers a queue worker's handle and subscribes it, keeping the two atomic: a SubscribeAsync failure
        // rolls back the partial registration (removes the id and disposes the worker) so a transient Redis error
        // can't wedge the id permanently with "a handle already exists" while nothing is actually subscribed (#655).
        private async Task SubscribeWithWorker(string channel, BackgroundWorker worker, string? id)
        {
            if (id is not null && !_handles.TryAdd(id, (handle, worker)))
            {
                worker.Dispose();
                throw new InvalidOperationException($"Cannot create handle with id: {id} because one already exists.");
            }

            try
            {
                await Subscriber.SubscribeAsync(RedisChannel.Literal(channel), handle);
            }
            catch
            {
                if (id is not null)
                {
                    _handles.TryRemove(id, out _);
                }
                worker.Dispose();
                throw;
            }

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
                handle.worker?.Dispose();
                await Subscriber.UnsubscribeAsync(RedisChannel.Literal(channel), handle.handler);
            }
        }
    }
}
