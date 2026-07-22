namespace Game.DataAccess
{
    /// <summary>
    /// Controls how a cross-instance reference-data-changed notification is turned into background cache
    /// reloads by <see cref="CoalescingReferenceCacheReloader"/>: notifications arriving within
    /// <see cref="DebounceWindow"/> of each other coalesce into a single reload sweep (a Workbench save
    /// fires several admin writes in quick succession), and a failed sweep is retried with exponential
    /// backoff per the base <see cref="RetryPolicy"/>. <see cref="ReconciliationInterval"/> additionally
    /// bounds how long an instance can go without a sweep at all, self-healing a notification Redis
    /// pub/sub's at-most-once delivery dropped (see #1980).
    /// </summary>
    internal sealed record ReferenceCacheReloadPolicy : RetryPolicy
    {
        public ReferenceCacheReloadPolicy(TimeSpan debounceWindow, int maxAttempts, TimeSpan baseDelay, TimeSpan? reconciliationInterval = null)
            : base(maxAttempts, baseDelay)
        {
            if (debounceWindow < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(debounceWindow), debounceWindow, "The debounce window cannot be negative.");
            }

            if (reconciliationInterval is { } interval && interval <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(reconciliationInterval), reconciliationInterval, "The reconciliation interval must be positive.");
            }

            DebounceWindow = debounceWindow;
            ReconciliationInterval = reconciliationInterval;
        }

        /// <summary>How long to wait after a notification before reloading, so an in-flight burst lands as one sweep.</summary>
        public TimeSpan DebounceWindow { get; }

        /// <summary>
        /// How long the reloader can go without a notification before it sweeps anyway, self-healing a signal
        /// this instance never received. <see langword="null"/> disables the periodic sweep (signal-only,
        /// today's behavior) — the default for every test policy in this suite unless a test opts in.
        /// </summary>
        public TimeSpan? ReconciliationInterval { get; }

        /// <summary>The default policy: a 500ms debounce window, five attempts with a 1s exponential backoff, and a 5-minute reconciliation sweep.</summary>
        public static ReferenceCacheReloadPolicy Default { get; } = new(
            debounceWindow: TimeSpan.FromMilliseconds(500),
            maxAttempts: 5,
            baseDelay: TimeSpan.FromSeconds(1),
            reconciliationInterval: TimeSpan.FromMinutes(5));
    }
}
