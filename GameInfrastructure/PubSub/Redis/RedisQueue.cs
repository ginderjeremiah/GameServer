using GameCore;
using GameCore.Infrastructure;
using StackExchange.Redis;

namespace GameInfrastructure.PubSub.Redis
{
    internal class RedisQueue : IPubSubQueue
    {
        private readonly IApiLogger _logger;
        private IDatabase Redis { get; }
        public string QueueName { get; }

        public RedisQueue(IDatabase redis, string queueName, IApiLogger logger)
        {
            Redis = redis;
            QueueName = queueName;
            _logger = logger;
        }

        public string? GetNext()
        {
            var value = Redis.ListLeftPop(QueueName);
            if (value.HasValue)
            {
                _logger.Log($"Retrieved value from RedisQueue: {QueueName}, value: {value}");
            }

            return value;
        }

        public T? GetNext<T>()
        {
            return GetNext().Deserialize<T>();
        }

        public async Task<string?> GetNextAsync()
        {
            var value = await Redis.ListLeftPopAsync(QueueName);
            if (value.HasValue)
            {
                _logger.Log($"Retrieved value from RedisQueue: {QueueName}, value: {value}");
            }

            return value;
        }

        public async Task<T?> GetNextAsync<T>()
        {
            var val = await GetNextAsync();
            return val.Deserialize<T>();
        }

        public void AddToQueue(string? value)
        {
            _logger.Log($"Adding value to RedisQueue: {QueueName}, value: {value}");
            Redis.ListRightPush(QueueName, value);
        }

        public void AddToQueue<T>(T value)
        {
            AddToQueue(value?.Serialize());
        }

        public Task AddToQueueAsync(string? value)
        {
            _logger.Log($"Adding value to RedisQueue: {QueueName}, value: {value}");
            return Redis.ListRightPushAsync(QueueName, value);
        }

        public Task AddToQueueAsync<T>(T value)
        {
            return AddToQueueAsync(value?.Serialize());
        }
    }
}
