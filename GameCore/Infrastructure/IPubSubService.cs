namespace GameCore.Infrastructure
{
    public interface IPubSubService
    {
        public void Publish(string channel, string message);
        public void Publish(string channel, string queueName, string? queueData);
        public void Publish<T>(string channel, string queueName, T queueData);
        public void Subscribe(string channel, Action<(string message, string channel)> action);
        public void Subscribe(string channel, string queueName, Action<(IPubSubQueue queue, string channel)> action);
        public void Subscribe(string channel, string queueName, Func<(IPubSubQueue queue, string channel), Task> action);
    }
}

