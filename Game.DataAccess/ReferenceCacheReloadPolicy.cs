namespace Game.DataAccess
{
    /// <summary>
    /// Controls how a cross-instance reference-data-changed notification is turned into background cache
    /// reloads by <see cref="CoalescingReferenceCacheReloader"/>: notifications arriving within
    /// <see cref="DebounceWindow"/> of each other coalesce into a single reload sweep (a Workbench save
    /// fires several admin writes in quick succession), and a failed sweep is retried with exponential
    /// backoff per the base <see cref="RetryPolicy"/>.
    /// </summary>
    internal sealed record ReferenceCacheReloadPolicy : RetryPolicy
    {
        public ReferenceCacheReloadPolicy(TimeSpan debounceWindow, int maxAttempts, TimeSpan baseDelay)
            : base(maxAttempts, baseDelay)
        {
            if (debounceWindow < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(debounceWindow), debounceWindow, "The debounce window cannot be negative.");
            }

            DebounceWindow = debounceWindow;
        }

        /// <summary>How long to wait after a notification before reloading, so an in-flight burst lands as one sweep.</summary>
        public TimeSpan DebounceWindow { get; }

        /// <summary>The default policy: a 500ms debounce window and five attempts with a 1s exponential backoff.</summary>
        public static ReferenceCacheReloadPolicy Default { get; } = new(
            debounceWindow: TimeSpan.FromMilliseconds(500),
            maxAttempts: 5,
            baseDelay: TimeSpan.FromSeconds(1));
    }
}
