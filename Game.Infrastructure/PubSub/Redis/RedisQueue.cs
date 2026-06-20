using Game.Abstractions.Infrastructure;
using Game.Core;
using Game.Infrastructure.Redis;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Game.Infrastructure.PubSub.Redis
{
    internal class RedisQueue : IPubSubQueue
    {
        // StackExchange.Redis exposes no CancellationToken on its database operations, so each async op honours the
        // per-command budget cooperatively via RedisCommandBudget (the same partial-honouring as RedisService): the
        // await unwinds promptly on cancellation while the underlying command settles in the background. Pure reads
        // (length/peek) take the read path; every mutating op (pop/reserve/acknowledge/reclaim/remove/push) takes
        // the write path, so a post-cancellation fault on an abandoned write is logged rather than lost.
        private const string WriteFaultMessage = "A Redis queue command faulted after its command budget was cancelled; the operation may not have been applied.";

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

        public async Task<string?> GetNextAsync(CancellationToken cancellationToken = default)
        {
            // LPOP is a destructive read (it removes the head), so it takes the write path: like RedisService.GetDelete
            // a post-cancellation fault on a read-and-remove would otherwise lose the popped item with no signal.
            var value = await ObserveWrite(_redis.ListLeftPopAsync(QueueName), cancellationToken);
            if (value.HasValue)
            {
                _logger.LogTrace("Retrieved value from RedisQueue: {QueueName}, value: {Value}", QueueName, value);
            }

            return value;
        }

        public async Task<T?> GetNextAsync<T>(CancellationToken cancellationToken = default)
        {
            var val = await GetNextAsync(cancellationToken);
            return val.Deserialize<T>();
        }

        public async Task<string?> ReserveNextAsync(CancellationToken cancellationToken = default)
        {
            // Honour an already-cancelled budget before reserving new work: a requested stop must not start
            // draining another item, and the up-front throw makes the reserve promptly cancelable rather than
            // relying on WaitAsync's racy already-complete check.
            cancellationToken.ThrowIfCancellationRequested();

            // LMOVE head->tail: atomically pop this queue's head and park it on the processing list, so the item
            // is never out of Redis between the read and a durable apply. Returns null when the queue is empty.
            var value = await ObserveWrite(_redis.ListMoveAsync(QueueName, ProcessingQueueName, ListSide.Left, ListSide.Right), cancellationToken);
            if (value.HasValue)
            {
                _logger.LogTrace("Reserved value from RedisQueue: {QueueName}, value: {Value}", QueueName, value);
            }

            return value;
        }

        public Task AcknowledgeAsync(string value, CancellationToken cancellationToken = default)
        {
            // Remove a single matching occurrence from the processing list (LREM count 1). A reserved item is
            // unique in flight, and a no-op (count 0) is fine when another run already reclaimed it.
            return ObserveWrite(_redis.ListRemoveAsync(ProcessingQueueName, value, 1), cancellationToken);
        }

        public async Task<long> ReclaimProcessingAsync(CancellationToken cancellationToken = default)
        {
            // Move each orphaned item from the processing list back onto this queue's head, tail-first, so their
            // original head-to-tail order is preserved (the oldest ends up at the head, ahead of newer items).
            long reclaimed = 0;
            while (true)
            {
                // Check the budget each pass so a stop requested mid-boot unwinds a long reclaim promptly rather
                // than only after the whole processing list has been moved.
                cancellationToken.ThrowIfCancellationRequested();
                var moved = await ObserveWrite(_redis.ListMoveAsync(ProcessingQueueName, QueueName, ListSide.Right, ListSide.Left), cancellationToken);
                if (!moved.HasValue)
                {
                    break;
                }

                reclaimed++;
            }

            if (reclaimed > 0)
            {
                _logger.LogTrace("Reclaimed {Count} orphaned value(s) into RedisQueue: {QueueName}", reclaimed, QueueName);
            }

            return reclaimed;
        }

        public Task<long> GetLengthAsync(CancellationToken cancellationToken = default)
        {
            return RedisCommandBudget.Read(_redis.ListLengthAsync(QueueName), cancellationToken);
        }

        public async Task<IReadOnlyList<string>> PeekAsync(long count, CancellationToken cancellationToken = default)
        {
            if (count <= 0)
            {
                return [];
            }

            // LRANGE 0..count-1 reads the oldest items at the head without removing them, so inspecting the
            // queue never risks the at-most-once loss a destructive pop would — the reason the dead-letter
            // queue is read this way rather than popped.
            var values = await RedisCommandBudget.Read(_redis.ListRangeAsync(QueueName, 0, count - 1), cancellationToken);
            return Array.ConvertAll(values, value => value.ToString());
        }

        public async Task<bool> RemoveAsync(string value, CancellationToken cancellationToken = default)
        {
            // LREM count 1 removes a single matching occurrence; returns false (a no-op) when none remain.
            return await ObserveWrite(_redis.ListRemoveAsync(QueueName, value, 1), cancellationToken) > 0;
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

        public Task AddToQueueAsync(string value, CancellationToken cancellationToken = default)
        {
            _logger.LogTrace("Added value to RedisQueue: {QueueName}, value: {Value}", QueueName, value);
            return ObserveWrite(_redis.ListRightPushAsync(QueueName, value), cancellationToken);
        }

        public Task AddToQueueAsync<T>(T value, CancellationToken cancellationToken = default)
        {
            return AddToQueueAsync(value.Serialize(), cancellationToken);
        }

        public Task AddRangeToQueueAsync(IEnumerable<string> values, CancellationToken cancellationToken = default)
        {
            var redisValues = values.Select(value => (RedisValue)value).ToArray();
            _logger.LogTrace("Added {Count} values to RedisQueue: {QueueName}", redisValues.Length, QueueName);
            return ObserveWrite(_redis.ListRightPushAsync(QueueName, redisValues), cancellationToken);
        }

        // Awaits a mutating command under the cancellation budget, attaching a fault-logging continuation so an
        // abandoned write that later faults — a silently failed write with no other signal — is surfaced not lost.
        private Task<T> ObserveWrite<T>(Task<T> command, CancellationToken cancellationToken)
        {
            return RedisCommandBudget.Write(command, cancellationToken, _logger, WriteFaultMessage);
        }
    }
}
