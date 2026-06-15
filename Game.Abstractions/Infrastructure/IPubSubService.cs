namespace Game.Abstractions.Infrastructure
{
    public interface IPubSubService
    {
        // The publish members take an optional CancellationToken (defaulting to none) so a write reachable on
        // the per-command path unwinds cooperatively on a cancelled budget (#558). The Subscribe/UnSubscribe
        // members deliberately take none: they register/remove app-lifetime subscriptions at startup/teardown,
        // never on a cancelable command path.
        public Task Publish(string channel, string message, CancellationToken cancellationToken = default);
        public Task Publish(string channel, string queueName, string queueData, CancellationToken cancellationToken = default);
        public Task Publish<T>(string channel, string queueName, T queueData, CancellationToken cancellationToken = default);

        /// <summary>
        /// Publishes a batch of queue items raised together (e.g. the player events from a single save) as
        /// one multi-value LPUSH onto <paramref name="queueName"/> followed by a single fire-and-forget wake
        /// on <paramref name="channel"/>, instead of one round-trip per item. A no-op when the batch is empty.
        /// </summary>
        public Task PublishBatch<T>(string channel, string queueName, IEnumerable<T> queueData, CancellationToken cancellationToken = default);
        public Task Subscribe(string channel, Action<(string message, string channel)> action, string? id = null);
        public Task Subscribe(string channel, string queueName, Action<(IPubSubQueue queue, string channel)> action, string? id = null);
        public Task Subscribe(string channel, string queueName, Func<(IPubSubQueue queue, string channel), Task> action, string? id = null);
        public Task UnSubscribe(string channel);
        public Task UnSubscribe(string channel, string id);

        /// <summary>
        /// Returns a handle to the named queue without subscribing to any channel. Useful for writing to a
        /// queue that has no live subscriber (e.g. a dead-letter queue) or for reading a queue on demand.
        /// </summary>
        public IPubSubQueue GetQueue(string queueName);
    }
}

