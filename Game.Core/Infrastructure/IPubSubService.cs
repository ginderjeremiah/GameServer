namespace Game.Core.Infrastructure
{
    public interface IPubSubService
    {
        public Task Publish(string channel, string message);
        public Task Publish(string channel, string queueName, string queueData);
        public Task Publish<T>(string channel, string queueName, T queueData);
        public Task Subscribe(string channel, Action<(string message, string channel)> action, string? id = null);
        public Task Subscribe(string channel, string queueName, Action<(IPubSubQueue queue, string channel)> action, string? id = null);
        public Task Subscribe(string channel, string queueName, Func<(IPubSubQueue queue, string channel), Task> action, string? id = null);
        public Task UnSubscribe(string channel);
        public Task UnSubscribe(string channel, string id);
    }
}

