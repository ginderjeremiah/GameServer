using GameLibrary;
using StackExchange.Redis;
using System.Reflection;

namespace DataAccess.Redis
{
    internal class RedisStore
    {
        private static RedisStore? _instance;
        private static readonly object _instanceLock = new();
        private ConnectionMultiplexer Multiplexer { get; }
        public IDatabase Redis => Multiplexer.GetDatabase();
        private RedisStore(IDataConfiguration config)
        {
            Multiplexer = ConnectionMultiplexer.Connect(config.RedisConnectionString);
            var subscriber = Multiplexer.GetSubscriber();
            var assembly = Assembly.GetAssembly(typeof(RedisStore));
            foreach (var type in assembly.GetTypes())
            {
                foreach (var method in type.GetMethods())
                {
                    var att = method.GetCustomAttribute<RedisSubscriberAttribute>();
                    if (att != null)
                    {
                        var channel = att.Channel;
                        var callback = method.CreateDelegate<Action<RepositoryManager, RedisValue>>();
                        subscriber.Subscribe(channel, (channel, value) => callback(new RepositoryManager(config), value));
                    }
                }
            }
        }

        public static RedisStore GetInstance(IDataConfiguration config)
        {
            if (_instance == null)
            {
                lock (_instanceLock)
                {
                    _instance ??= new RedisStore(config);
                }
            }
            return _instance;
        }


        public T? Get<T>(string key)
        {
            return Redis.StringGet(key).Deserialize<T>();
        }

        //public async Task<T?> GetAsync<T>(string key)
        //{
        //    return (await Redis.StringGetAsync(key)).Deserialize<T>();
        //}

        public bool TryGet<T>(string key, out T result)
        {
            result = Get<T>(key);
            return result is not null;
        }

        public void SetAndForget<T>(string key, T value)
        {
            SetAndForget(key, value.Serialize());
        }

        public void SetAndForget(string key, string value)
        {
            StringSet(key, value, CommandFlags.FireAndForget);
        }

        public void Set<T>(string key, T value)
        {
            Set(key, value.Serialize());
        }
        public void Set(string key, string value)
        {
            StringSet(key, value);
        }

        private void StringSet(string key, string value, CommandFlags flags = CommandFlags.None)
        {
            Redis.StringSet(key, value, flags: flags);
        }

        //public async Task SetAsync<T>(string key, T value)
        //{
        //    await SetAsync(key, value.Serialize());
        //}

        //public async Task SetAsync<T>(string key, string value)
        //{
        //    await Redis.StringSetAsync(key, value);
        //}

        public void Publish(string channelName, object value)
        {
            Publish(channelName, value.Serialize());
        }

        public void Publish(string channelName, string value)
        {
            Redis.Publish(RedisChannel.Literal(channelName), value);
        }

        public T? GetFromQueue<T>(string queueName)
        {
            return Redis.ListLeftPop(queueName).Deserialize<T>();
        }

        public bool TryGetFromQueue<T>(string queueName, out T value)
        {
            value = GetFromQueue<T>(queueName);
            return value is not null;
        }

        //public async Task<T?> GetFromQueueAsync<T>(string queueName)
        //{
        //    return (await Redis.ListLeftPopAsync(queueName)).Deserialize<T>();
        //}

        public void AddToQueue<T>(string queueName, T value)
        {
            AddToQueue(queueName, value.Serialize());
        }

        public void AddToQueue(string queueName, string value)
        {
            Redis.ListRightPush(queueName, value);
        }
    }
}
