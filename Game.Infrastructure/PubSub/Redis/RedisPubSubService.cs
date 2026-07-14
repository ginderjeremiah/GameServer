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
        // Records the channel a handle was subscribed on, so UnSubscribe never has to trust a caller-supplied
        // channel that might not match (#1825).
        private static readonly ConcurrentDictionary<string, (string channel, Action<RedisChannel, RedisValue> handler, BackgroundWorker? worker)> _handles = [];

        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<RedisPubSubService> _logger;

        // The multiplexer is fixed for the instance lifetime, so the database/subscriber handles it hands out are
        // resolved once in the ctor rather than per call on the hot publish/subscribe paths (#954).
        public IDatabase Redis { get; }
        public ISubscriber Subscriber { get; }

        public RedisPubSubService(IConnectionMultiplexer multiplexer, ILoggerFactory loggerFactory)
        {
            Redis = multiplexer.GetDatabase();
            Subscriber = multiplexer.GetSubscriber();
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<RedisPubSubService>();
        }

        // Deliberately NOT fire-and-forget (#1888): this overload backs broadcasts with no durable write behind
        // them (e.g. the reference-data invalidation channel), so a genuine send failure must propagate to the
        // caller rather than vanish silently the way CommandFlags.FireAndForget would swallow it. Contrast with
        // Wake below, whose fire-and-forget is safe because a durable queue write already backs it.
        public async Task Publish(string channel, string message, CancellationToken cancellationToken = default)
        {
            await Redis.PublishAsync(RedisChannel.Literal(channel), message).WaitAsync(cancellationToken);
        }

        // A bare channel wake: a fire-and-forget empty publish that only nudges the consumer to drain its queue.
        // The durable enqueue happens elsewhere (or has already happened); Redis pub/sub is at-most-once so this is
        // only a hint, and the consumer drains the whole queue on its next wake regardless, so it needs no
        // cancellation token (#552). A dedicated method keeps the wake from being confused with a queue enqueue.
        public Task Wake(string channel)
        {
            return Redis.PublishAsync(RedisChannel.Literal(channel), "", CommandFlags.FireAndForget);
        }

        public IPubSubQueue GetQueue(string queueName)
        {
            return new RedisQueue(Redis, queueName, _loggerFactory.CreateLogger<RedisQueue>());
        }

        public async Task Publish(string channel, string queueName, string queueData, CancellationToken cancellationToken = default)
        {
            var queue = GetQueue(queueName);
            // The queue write is the durable part and stays awaited under the cancellation budget (the queue op
            // honours it cooperatively and logs an abandoned-write fault); the channel wake is only a
            // fire-and-forget signal for the consumer (the data is safely enqueued regardless, and the consumer
            // drains the whole queue on its next wake) (#552).
            await queue.AddToQueueAsync(queueData, cancellationToken);
            await Wake(channel);
        }

        public async Task Publish<T>(string channel, string queueName, T queueData, CancellationToken cancellationToken = default)
        {
            await Publish(channel, queueName, queueData.Serialize(), cancellationToken);
        }

        public async Task PublishBatch<T>(string channel, string queueName, IEnumerable<T> queueData, CancellationToken cancellationToken = default)
        {
            var values = queueData.Select(data => data.Serialize()).ToArray();
            if (values.Length == 0)
            {
                return;
            }

            // One multi-value LPUSH carries the whole batch durably (under the cancellation budget the queue op
            // honours cooperatively); the single wake is fire-and-forget for the same reason as the per-event
            // Publish above — the data is already enqueued and the consumer drains the whole queue on its next
            // wake (#559).
            var queue = GetQueue(queueName);
            await queue.AddRangeToQueueAsync(values, cancellationToken);
            await Wake(channel);
        }

        public async Task Subscribe(string channel, Action<(string message, string channel)> action, string? id = null)
        {
            _logger.LogInformation("Creating redis subscriber on channel '{Channel}'.", channel);
            if (id is not null && !_handles.TryAdd(id, (channel, handle, null)))
            {
                throw new InvalidOperationException($"Cannot create handle with id: {id} because one already exists.");
            }

            // Keep registration and subscribe atomic: a SubscribeAsync failure rolls back the id so a transient
            // Redis error can't wedge it permanently with "a handle already exists" while nothing is subscribed (#655).
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
                throw;
            }

            void handle(RedisChannel _, RedisValue message)
            {
                action((message.ToString(), channel));
            }
        }

        public async Task Subscribe(string channel, string queueName, Func<(IPubSubQueue queue, string channel), Task> action, string id)
        {
            // Validate the id before creating the worker so a misuse can't leak the worker's wait handle (#954).
            ArgumentNullException.ThrowIfNull(id);
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
        // The id is required: a worker tracked under no id could never be disposed via UnSubscribe and would leak
        // its OS wait handle (#954).
        private async Task SubscribeWithWorker(string channel, BackgroundWorker worker, string id)
        {
            if (!_handles.TryAdd(id, (channel, handle, worker)))
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
                _handles.TryRemove(id, out _);
                worker.Dispose();
                throw;
            }

            void handle(RedisChannel _c, RedisValue _v)
            {
                worker.Start();
            }
        }

        public async Task UnSubscribe(string id)
        {
            if (_handles.TryRemove(id, out var handle))
            {
                // Unsubscribe the handler before disposing the worker, not after. The handler is worker.Start(),
                // so disposing first leaves a window where a message still routed to the (now-removed-but-still-
                // subscribed) handler calls Start() on a disposed worker — an ObjectDisposedException thrown on the
                // StackExchange.Redis subscriber thread. Removing the subscription first closes that window.
                // The channel comes from the registered handle, not a caller-supplied argument, so it can never
                // mismatch the channel the handler was actually subscribed on (#1825).
                await Subscriber.UnsubscribeAsync(RedisChannel.Literal(handle.channel), handle.handler);
                handle.worker?.Dispose();
            }
        }
    }
}
