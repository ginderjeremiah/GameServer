namespace Game.DataAccess
{
    /// <summary>
    /// Controls how <see cref="DataProviderSynchronizer"/> retries a failed player-update event before
    /// moving it to the dead-letter queue. <see cref="BaseDelay"/> is the wait after the first failed
    /// attempt; each subsequent wait doubles (exponential backoff).
    /// </summary>
    internal sealed record PlayerUpdateRetryPolicy
    {
        public PlayerUpdateRetryPolicy(int maxAttempts, TimeSpan baseDelay)
        {
            if (maxAttempts < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxAttempts), maxAttempts, "A retry policy must allow at least one attempt.");
            }

            if (baseDelay < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(baseDelay), baseDelay, "The backoff delay cannot be negative.");
            }

            MaxAttempts = maxAttempts;
            BaseDelay = baseDelay;
        }

        /// <summary>The total number of times an event is attempted before it is dead-lettered.</summary>
        public int MaxAttempts { get; }

        /// <summary>The backoff delay after the first failed attempt; doubled for each subsequent attempt.</summary>
        public TimeSpan BaseDelay { get; }

        /// <summary>The default policy: three attempts with a 200ms exponential backoff.</summary>
        public static PlayerUpdateRetryPolicy Default { get; } = new(maxAttempts: 3, baseDelay: TimeSpan.FromMilliseconds(200));

        /// <summary>
        /// The backoff delay to wait after <paramref name="failedAttempt"/> (1-based) before the next attempt.
        /// </summary>
        public TimeSpan DelayAfterAttempt(int failedAttempt)
        {
            if (failedAttempt < 1)
            {
                return TimeSpan.Zero;
            }

            return BaseDelay * Math.Pow(2, failedAttempt - 1);
        }
    }
}
