namespace Game.Abstractions.Infrastructure
{
    public interface IPubSubQueue
    {
        public Task<string?> GetNextAsync(CancellationToken cancellationToken = default);
        public Task<T?> GetNextAsync<T>(CancellationToken cancellationToken = default);

        /// <summary>
        /// Atomically moves the next item from the head of this queue onto a side processing list and returns it
        /// (or null when the queue is empty), making the read at-least-once: the item is durably parked rather
        /// than destructively popped, so a crash mid-processing leaves it recoverable instead of lost.
        /// Acknowledge it with <see cref="AcknowledgeAsync"/> once it has been durably applied, and recover items
        /// orphaned by a crashed run with <see cref="ReclaimProcessingAsync"/>.
        /// </summary>
        public Task<string?> ReserveNextAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes an item reserved by <see cref="ReserveNextAsync"/> from the processing list once it has been
        /// durably applied (the acknowledgement). Acknowledging an item already gone (e.g. one another run has
        /// reclaimed) is a no-op.
        /// </summary>
        public Task AcknowledgeAsync(string value, CancellationToken cancellationToken = default);

        /// <summary>
        /// Moves every item left on the processing list — orphaned by a previous run crashing between reserving
        /// and acknowledging, or stranded by a live consumer's own acknowledge/dead-letter path faulting — back
        /// onto the head of this queue in their original order, so they are re-processed rather than lost.
        /// Returns the number of items reclaimed.
        /// </summary>
        public Task<long> ReclaimProcessingAsync(CancellationToken cancellationToken = default);

        /// <summary>The current number of items waiting on this queue (e.g. to surface dead-letter-queue depth).</summary>
        public Task<long> GetLengthAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// The current number of items reserved via <see cref="ReserveNextAsync"/> but not yet acknowledged —
        /// the size of the side processing list. Lets a caller detect items stranded there (so they can be
        /// reclaimed opportunistically) without mutating anything, unlike <see cref="ReclaimProcessingAsync"/>.
        /// </summary>
        public Task<long> GetProcessingCountAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns up to <paramref name="count"/> items from the head of the queue (oldest first) WITHOUT
        /// removing them — a non-destructive read. Lets a dead-letter queue be inspected without the
        /// at-most-once exposure a destructive pop would reintroduce. A non-positive count returns an empty list.
        /// </summary>
        public Task<IReadOnlyList<string>> PeekAsync(long count, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns up to <paramref name="count"/> items from the head of the processing list (oldest
        /// reserved first) WITHOUT removing them — the processing-list counterpart to <see cref="PeekAsync"/>.
        /// Lets a caller identify the specific item currently stranded there (rather than merely that the list
        /// is non-empty), so it can detect whether that same item is still there on a later poll. A
        /// non-positive count returns an empty list.
        /// </summary>
        public Task<IReadOnlyList<string>> PeekProcessingAsync(long count, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes a single occurrence of <paramref name="value"/> from this queue, returning true when one
        /// was removed. Used to acknowledge a dead-letter entry off the queue once it has been re-enqueued
        /// for replay; a no-op (false) when no matching entry remains.
        /// </summary>
        public Task<bool> RemoveAsync(string value, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes one occurrence of each value in <paramref name="values"/> from this queue in a single
        /// round trip — the batched counterpart to calling <see cref="RemoveAsync"/> once per value, for a
        /// caller acknowledging many entries at once (e.g. a bulk dead-letter replay) that would otherwise pay
        /// one sequential network round trip per entry. A value repeated in <paramref name="values"/> removes
        /// that many occurrences; a value no longer present is silently skipped, same as
        /// <see cref="RemoveAsync"/>'s no-op semantics. Returns the number of values actually removed. The
        /// caller is responsible for not passing an empty sequence.
        /// </summary>
        public Task<long> RemoveRangeAsync(IReadOnlyList<string> values, CancellationToken cancellationToken = default);

        public Task AddToQueueAsync(string value, CancellationToken cancellationToken = default);
        public Task AddToQueueAsync<T>(T value, CancellationToken cancellationToken = default);

        /// <summary>
        /// Pushes multiple values onto the queue in a single round-trip (one multi-value LPUSH),
        /// preserving their order. The caller is responsible for not passing an empty sequence.
        /// Named distinctly from <see cref="AddToQueueAsync{T}(T)"/> so passing a concrete
        /// collection (e.g. <c>string[]</c>) can't bind to the single-value generic overload instead.
        /// </summary>
        public Task AddRangeToQueueAsync(IEnumerable<string> values, CancellationToken cancellationToken = default);
    }
}
