using StackExchange.Redis;
using System.Diagnostics.CodeAnalysis;

namespace GameCore.PubSub.Redis
{
    internal class RedisQueue : IPubSubQueue
    {
        private IDatabase Redis { get; }
        public string QueueName { get; }

        public RedisQueue(IDatabase redis, string queueName)
        {
            Redis = redis;
            QueueName = queueName;
        }

        public string? GetNext()
        {
            return Redis.ListLeftPop(QueueName);
        }

        public T? GetNext<T>()
        {
            return GetNext().Deserialize<T>();
        }

        public async Task<string?> GetNextAsync()
        {
            return await Redis.ListLeftPopAsync(QueueName);
        }

        public async Task<T?> GetNextAsync<T>()
        {
            var val = await GetNextAsync();
            return val.Deserialize<T>();
        }

        public bool TryGetNext([NotNullWhen(true)] out string? value)
        {
            value = GetNext();
            return value is not null;
        }

        public bool TryGetNext<T>([NotNullWhen(true)] out T? value)
        {
            value = GetNext<T>();
            return value is not null;
        }

        public void AddToQueue(string? value)
        {
            Redis.ListRightPush(QueueName, value);
        }

        public void AddToQueue<T>(T value)
        {
            AddToQueue(value.Serialize());
        }

        public Task AddToQueueAsync(string value)
        {
            return Redis.ListRightPushAsync(QueueName, value);
        }

        public Task AddToQueueAsync<T>(T value)
        {
            return AddToQueueAsync(value.Serialize());
        }
    }
}
