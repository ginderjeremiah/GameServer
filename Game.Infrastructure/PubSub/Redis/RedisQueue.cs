using Game.Abstractions.Infrastructure;
using Game.Core;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Game.Infrastructure.PubSub.Redis
{
    internal class RedisQueue : IPubSubQueue
    {
        private readonly ILogger<RedisQueue> _logger;
        private readonly IDatabase _redis;

        public string QueueName { get; }

        // Side list holding items reserved for processing but not yet acknowledged. The key is shared (not
        // per-instance) so any instance's startup reclaim recovers items a crashed run left in flight;
        // re-processing a reclaimed item is safe because the write-behind handlers are idempotent.
        private string ProcessingQueueName => $"{QueueName}:processing";

        public RedisQueue(IDatabase redis, string queueName, ILogger<RedisQueue> logger)
        {
            _redis = redis;
            QueueName = queueName;
            _logger = logger;
        }

        public string? GetNext()
        {
            var value = _redis.ListLeftPop(QueueName);
            if (value.HasValue)
            {
                _logger.LogTrace("Retrieved value from RedisQueue: {QueueName}, value: {Value}", QueueName, value);
            }

            return value;
        }

        public T? GetNext<T>()
        {
            return GetNext().Deserialize<T>();
        }

        public async Task<string?> GetNextAsync()
        {
            var value = await _redis.ListLeftPopAsync(QueueName);
            if (value.HasValue)
            {
                _logger.LogTrace("Retrieved value from RedisQueue: {QueueName}, value: {Value}", QueueName, value);
            }

            return value;
        }

        public async Task<T?> GetNextAsync<T>()
        {
            var val = await GetNextAsync();
            return val.Deserialize<T>();
        }

        public async Task<string?> ReserveNextAsync()
        {
            // LMOVE head->tail: atomically pop this queue's head and park it on the processing list, so the item
            // is never out of Redis between the read and a durable apply. Returns null when the queue is empty.
            var value = await _redis.ListMoveAsync(QueueName, ProcessingQueueName, ListSide.Left, ListSide.Right);
            if (value.HasValue)
            {
                _logger.LogTrace("Reserved value from RedisQueue: {QueueName}, value: {Value}", QueueName, value);
            }

            return value;
        }

        public Task AcknowledgeAsync(string value)
        {
            // Remove a single matching occurrence from the processing list (LREM count 1). A reserved item is
            // unique in flight, and a no-op (count 0) is fine when another run already reclaimed it.
            return _redis.ListRemoveAsync(ProcessingQueueName, value, 1);
        }

        public async Task<long> ReclaimProcessingAsync()
        {
            // Move each orphaned item from the processing list back onto this queue's head, tail-first, so their
            // original head-to-tail order is preserved (the oldest ends up at the head, ahead of newer items).
            long reclaimed = 0;
            while ((await _redis.ListMoveAsync(ProcessingQueueName, QueueName, ListSide.Right, ListSide.Left)).HasValue)
            {
                reclaimed++;
            }

            if (reclaimed > 0)
            {
                _logger.LogTrace("Reclaimed {Count} orphaned value(s) into RedisQueue: {QueueName}", reclaimed, QueueName);
            }

            return reclaimed;
        }

        public Task<long> GetLengthAsync()
        {
            return _redis.ListLengthAsync(QueueName);
        }

        public async Task<IReadOnlyList<string>> PeekAsync(long count)
        {
            if (count <= 0)
            {
                return [];
            }

            // LRANGE 0..count-1 reads the oldest items at the head without removing them, so inspecting the
            // queue never risks the at-most-once loss a destructive pop would — the reason the dead-letter
            // queue is read this way rather than popped.
            var values = await _redis.ListRangeAsync(QueueName, 0, count - 1);
            return Array.ConvertAll(values, value => value.ToString());
        }

        public async Task<bool> RemoveAsync(string value)
        {
            // LREM count 1 removes a single matching occurrence; returns false (a no-op) when none remain.
            return await _redis.ListRemoveAsync(QueueName, value, 1) > 0;
        }

        public void AddToQueue(string value)
        {
            _logger.LogTrace("Added value to RedisQueue: {QueueName}, value: {Value}", QueueName, value);
            _redis.ListRightPush(QueueName, value);
        }

        public void AddToQueue<T>(T value)
        {
            AddToQueue(value.Serialize());
        }

        public Task AddToQueueAsync(string value)
        {
            _logger.LogTrace("Added value to RedisQueue: {QueueName}, value: {Value}", QueueName, value);
            return _redis.ListRightPushAsync(QueueName, value);
        }

        public Task AddToQueueAsync<T>(T value)
        {
            return AddToQueueAsync(value.Serialize());
        }

        public Task AddRangeToQueueAsync(IEnumerable<string> values)
        {
            var redisValues = values.Select(value => (RedisValue)value).ToArray();
            _logger.LogTrace("Added {Count} values to RedisQueue: {QueueName}", redisValues.Length, QueueName);
            return _redis.ListRightPushAsync(QueueName, redisValues);
        }
    }
}
