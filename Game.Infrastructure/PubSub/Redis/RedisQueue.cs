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
