using GameLibrary;
using StackExchange.Redis;

namespace DataAccess.Redis
{
    internal class RedisQueue
    {
        private readonly RedisStore _redisStore;
        private IDatabase Redis => _redisStore.Redis;
        public RedisKey QueueName { get; }
        public RedisChannel SubscriberChannel { get; }

        public RedisQueue(RedisStore redisStore, RedisKey queueName, RedisChannel subscriberChannel)
        {
            _redisStore = redisStore;
            QueueName = queueName;
            SubscriberChannel = subscriberChannel;
        }

        public RedisValue GetFromQueue()
        {
            return Redis.ListLeftPop(QueueName);
        }

        public T? GetFromQueue<T>()
        {
            return Redis.ListLeftPop(QueueName).Deserialize<T>();
        }

        public bool TryGetFromQueue(out RedisValue value)
        {
            value = GetFromQueue();
            return value.HasValue;
        }

        public bool TryGetFromQueue<T>(out T value)
        {
            value = GetFromQueue<T>();
            return value is not null;
        }

        public void AddToQueue<T>(T value)
        {
            AddToQueue(value.Serialize());
        }

        public void AddToQueue(string value)
        {
            Redis.ListRightPush(QueueName, value);
            Redis.Publish(SubscriberChannel, "");
        }
    }
}
