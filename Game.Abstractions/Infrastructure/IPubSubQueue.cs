namespace Game.Abstractions.Infrastructure
{
    public interface IPubSubQueue
    {
        public string? GetNext();
        public T? GetNext<T>();
        public Task<string?> GetNextAsync();
        public Task<T?> GetNextAsync<T>();

        /// <summary>
        /// Atomically moves the next item from the head of this queue onto a side processing list and returns it
        /// (or null when the queue is empty), making the read at-least-once: the item is durably parked rather
        /// than destructively popped, so a crash mid-processing leaves it recoverable instead of lost.
        /// Acknowledge it with <see cref="AcknowledgeAsync"/> once it has been durably applied, and recover items
        /// orphaned by a crashed run with <see cref="ReclaimProcessingAsync"/>.
        /// </summary>
        public Task<string?> ReserveNextAsync();

        /// <summary>
        /// Removes an item reserved by <see cref="ReserveNextAsync"/> from the processing list once it has been
        /// durably applied (the acknowledgement). Acknowledging an item already gone (e.g. one another run has
        /// reclaimed) is a no-op.
        /// </summary>
        public Task AcknowledgeAsync(string value);

        /// <summary>
        /// Moves every item left on the processing list — orphaned when a previous run crashed between reserving
        /// and acknowledging — back onto the head of this queue in their original order, so they are re-processed
        /// rather than stranded. Returns the number of items reclaimed.
        /// </summary>
        public Task<long> ReclaimProcessingAsync();

        /// <summary>The current number of items waiting on this queue (e.g. to surface dead-letter-queue depth).</summary>
        public Task<long> GetLengthAsync();

        public void AddToQueue(string value);
        public void AddToQueue<T>(T value);
        public Task AddToQueueAsync(string value);
        public Task AddToQueueAsync<T>(T value);

        /// <summary>
        /// Pushes multiple values onto the queue in a single round-trip (one multi-value LPUSH),
        /// preserving their order. The caller is responsible for not passing an empty sequence.
        /// Named distinctly from <see cref="AddToQueueAsync{T}(T)"/> so passing a concrete
        /// collection (e.g. <c>string[]</c>) can't bind to the single-value generic overload instead.
        /// </summary>
        public Task AddRangeToQueueAsync(IEnumerable<string> values);
    }
}
