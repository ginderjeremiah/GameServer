using Game.Abstractions.Infrastructure;

namespace Game.TestInfrastructure.Helpers
{
    /// <summary>
    /// Base <see cref="IPubSubService"/> test fake whose every member throws <see cref="NotSupportedException"/>.
    /// A test double derives from it and overrides only the members it means to exercise, so its intent stays
    /// obvious and any accidental call to an unexercised member fails loudly instead of silently no-oping.
    /// </summary>
    public abstract class NotSupportedPubSubService : IPubSubService
    {
        public virtual Task Publish(string channel, string message, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public virtual Task Publish(string channel, string queueName, string queueData, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public virtual Task Publish<T>(string channel, string queueName, T queueData, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public virtual Task PublishBatch<T>(string channel, string queueName, IEnumerable<T> queueData, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public virtual Task Wake(string channel) => throw new NotSupportedException();
        public virtual Task Subscribe(string channel, Action<(string message, string channel)> action, string? id = null) => throw new NotSupportedException();
        public virtual Task Subscribe(string channel, string queueName, Func<(IPubSubQueue queue, string channel), Task> action, string id) => throw new NotSupportedException();
        public virtual Task UnSubscribe(string id) => throw new NotSupportedException();
        public virtual IPubSubQueue GetQueue(string queueName) => throw new NotSupportedException();
    }
}
