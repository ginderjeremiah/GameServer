namespace Game.DataAccess
{
    /// <summary>
    /// Base shape for an exponential-backoff retry policy: an operation is attempted up to
    /// <see cref="MaxAttempts"/> times, waiting <see cref="BaseDelay"/> after the first failed attempt and
    /// doubling the wait after each subsequent one.
    /// </summary>
    internal abstract record RetryPolicy
    {
        protected RetryPolicy(int maxAttempts, TimeSpan baseDelay)
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

        /// <summary>The total number of times an operation is attempted before giving up.</summary>
        public int MaxAttempts { get; }

        /// <summary>The backoff delay after the first failed attempt; doubled for each subsequent attempt.</summary>
        public TimeSpan BaseDelay { get; }

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
