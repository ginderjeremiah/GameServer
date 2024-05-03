﻿using GameLibrary;
using GameLibrary.Database.Interfaces;
using GameLibrary.Logging;
using StackExchange.Redis;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace DataAccess.Redis
{
    internal class RedisStore
    {
        private static RedisStore? _instance;
        private static readonly object _instanceLock = new();

        private List<RedisQueue> Queues { get; } = new();
        private ConnectionMultiplexer Multiplexer { get; }
        public IDatabase Redis => Multiplexer.GetDatabase();

        private RedisStore(IDataConfiguration config, IApiLogger logger, IDataProvider database)
        {
            Multiplexer = ConnectionMultiplexer.Connect(config.RedisConnectionString);
            InitializeSubscribers(config, logger, database);
        }

        public static RedisStore GetInstance(IDataConfiguration config, IApiLogger logger, IDataProvider database)
        {
            if (_instance == null)
            {
                lock (_instanceLock)
                {
                    _instance ??= new RedisStore(config, logger, database);
                }
            }
            return _instance;
        }
        public string? Get(string key)
        {
            return Redis.StringGet(key);
        }

        public T? Get<T>(string key)
        {
            return Redis.StringGet(key).Deserialize<T>();
        }

        //public async Task<T?> GetAsync<T>(string key)
        //{
        //    return (await Redis.StringGetAsync(key)).Deserialize<T>();
        //}
        public string? GetDelete(string key)
        {
            return Redis.StringGetDelete(key);
        }

        public T? GetDelete<T>(string key)
        {
            return Redis.StringGetDelete(key).Deserialize<T>();
        }

        public bool TryGet<T>(string key, [NotNullWhen(true)] out T? result)
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

        //public void Publish(string channelName, object value)
        //{
        //    Publish(channelName, value.Serialize());
        //}

        //public void Publish(string channelName, string value)
        //{
        //    Redis.Publish(RedisChannel.Literal(channelName), value);
        //}

        public bool TryGetQueue(string queueName, [NotNullWhen(true)] out RedisQueue? queue)
        {
            queue = Queues.FirstOrDefault(q => q.QueueName == queueName);
            return queue is not null;
        }

        private void InitializeSubscribers(IDataConfiguration config, IApiLogger logger, IDataProvider database)
        {
            var subscriber = Multiplexer.GetSubscriber();
            var assembly = Assembly.GetAssembly(typeof(RedisStore));
            foreach (var type in assembly.GetTypes())
            {
                foreach (var method in type.GetMethods(BindingFlags.Static | BindingFlags.NonPublic))
                {
                    var att = method.GetCustomAttribute<RedisSubscriberAttribute>();
                    if (att != null)
                    {
                        var callbackDelegate = Delegate.CreateDelegate(typeof(Action<RepositoryManager, RedisValue>), method, false);
                        if (callbackDelegate is Action<RepositoryManager, RedisValue> callback)
                        {
                            var queue = new RedisQueue(this, att.QueueName, att.Channel);
                            Queues.Add(queue);
                            var worker = new RedisSubscriberWorker(config, queue, callback, logger, database);
                            subscriber.Subscribe(att.Channel, (channel, value) => worker.Start());
                        }
                    }
                }
            }
        }
    }
}
