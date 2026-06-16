namespace Game.DataAccess
{
    /// <summary>
    /// Base shape for an exponential-backoff retry policy: an operation is attempted up to
    /// <see cref="MaxAttempts"/> times, waiting <see cref="BaseDelay"/> after the first failed attempt and
    /// doubling the wait after each subsequent one, capped at <see cref="MaxDelay"/>.
    /// </summary>
    internal abstract record RetryPolicy
    {
        /// <summary>
        /// A generous default cap so the exponential backoff can't overflow <see cref="TimeSpan"/> (its
        /// multiply throws on overflow) or schedule an absurd wait for a policy configured with a high
        /// <see cref="MaxAttempts"/>; chosen well above every configured policy's natural ceiling.
        /// </summary>
        private static readonly TimeSpan DefaultMaxDelay = TimeSpan.FromMinutes(1);

        protected RetryPolicy(int maxAttempts, TimeSpan baseDelay, TimeSpan? maxDelay = null)
        {
            if (maxAttempts < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxAttempts), maxAttempts, "A retry policy must allow at least one attempt.");
            }

            if (baseDelay < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(baseDelay), baseDelay, "The backoff delay cannot be negative.");
            }

            var resolvedMaxDelay = maxDelay ?? DefaultMaxDelay;
            if (resolvedMaxDelay < baseDelay)
            {
                throw new ArgumentOutOfRangeException(nameof(maxDelay), maxDelay, "The maximum delay cannot be less than the base delay.");
            }

            MaxAttempts = maxAttempts;
            BaseDelay = baseDelay;
            MaxDelay = resolvedMaxDelay;
        }

        /// <summary>The total number of times an operation is attempted before giving up.</summary>
        public int MaxAttempts { get; }

        /// <summary>The backoff delay after the first failed attempt; doubled for each subsequent attempt.</summary>
        public TimeSpan BaseDelay { get; }

        /// <summary>The ceiling the doubling backoff saturates at, guarding against overflow and absurd waits.</summary>
        public TimeSpan MaxDelay { get; }

        /// <summary>
        /// The backoff delay to wait after <paramref name="failedAttempt"/> (1-based) before the next attempt,
        /// saturating at <see cref="MaxDelay"/>.
        /// </summary>
        public TimeSpan DelayAfterAttempt(int failedAttempt)
        {
            if (failedAttempt < 1)
            {
                return TimeSpan.Zero;
            }

            // Compute in double ticks and clamp before constructing the TimeSpan, so a large exponent
            // saturates at MaxDelay instead of overflowing the TimeSpan multiply (which throws). A huge or
            // infinite double still compares >= MaxDelay.Ticks, so it clamps cleanly.
            var ticks = BaseDelay.Ticks * Math.Pow(2, failedAttempt - 1);
            return ticks >= MaxDelay.Ticks
                ? MaxDelay
                : TimeSpan.FromTicks((long)ticks);
        }
    }
}
