namespace Game.DataAccess
{
    /// <summary>
    /// Controls how <see cref="DataProviderSynchronizer"/> retries a failed player-update event before
    /// moving it to the dead-letter queue.
    /// </summary>
    internal sealed record PlayerUpdateRetryPolicy : RetryPolicy
    {
        public PlayerUpdateRetryPolicy(int maxAttempts, TimeSpan baseDelay)
            : base(maxAttempts, baseDelay) { }

        /// <summary>The default policy: three attempts with a 200ms exponential backoff.</summary>
        public static PlayerUpdateRetryPolicy Default { get; } = new(maxAttempts: 3, baseDelay: TimeSpan.FromMilliseconds(200));
    }
}
