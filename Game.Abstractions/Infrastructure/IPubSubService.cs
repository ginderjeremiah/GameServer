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

        /// <summary>
        /// Sends a bare wake on <paramref name="channel"/>: a fire-and-forget empty publish that only nudges the
        /// channel's consumer to drain its queue, carrying no message of its own. Kept distinct from the
        /// <c>Publish</c> overloads so a wake can never be confused with — or a future edit silently turn it into —
        /// a queue enqueue. Takes no cancellation token: the wake is fire-and-forget (Redis pub/sub is at-most-once
        /// and the consumer drains the whole queue on its next wake), so there is nothing to unwind.
        /// </summary>
        public Task Wake(string channel);
        public Task Subscribe(string channel, Action<(string message, string channel)> action, string? id = null);

        // The worker-backed overload requires a non-null id: it spins up a BackgroundWorker (holding an OS wait
        // handle) that can only be disposed via UnSubscribe(id), so an id-less worker subscription could
        // never be torn down and would leak its handle (#954). The plain-action overload above creates no worker,
        // so its id stays optional.
        public Task Subscribe(string channel, string queueName, Func<(IPubSubQueue queue, string channel), Task> action, string id);

        // Takes only the id, not the channel: the channel a handle was subscribed on is recorded at subscribe
        // time, so a caller can never unsubscribe against a mismatched channel and leave the real subscription
        // routing to a disposed worker (#1825).
        public Task UnSubscribe(string id);

        /// <summary>
        /// Returns a handle to the named queue without subscribing to any channel. Useful for writing to a
        /// queue that has no live subscriber (e.g. a dead-letter queue) or for reading a queue on demand.
        /// </summary>
        public IPubSubQueue GetQueue(string queueName);
    }
}

