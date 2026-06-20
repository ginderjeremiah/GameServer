namespace Game.Application.Auth
{
    /// <summary>
    /// Configuration for the per-account login backoff (<see cref="LoginBackoffPolicy"/>), bound from the
    /// "LoginBackoff" section. Like the IP rate limiter — and unlike the CORS origins — these carry safe
    /// non-empty defaults so an unconfigured deployment is still protected; a deployment only overrides them
    /// to tune the curve. Validated as sane on start so a misconfiguration fails fast rather than silently
    /// disabling or inverting the guard.
    /// </summary>
    public class LoginBackoffOptions
    {
        /// <summary>The configuration section this options class binds from.</summary>
        public const string SectionName = "LoginBackoff";

        /// <summary>
        /// Consecutive failures allowed before any delay is applied, so honest fat-fingering is never slowed.
        /// </summary>
        public int FailureThreshold { get; set; } = 5;

        /// <summary>The delay applied on the first failure past the threshold; it doubles each further failure.</summary>
        public int BaseDelaySeconds { get; set; } = 1;

        /// <summary>
        /// The ceiling the doubling delay is capped at, keeping it a bounded slowdown rather than a lockout —
        /// a legitimate user is never delayed by more than this even while their account is under attack.
        /// </summary>
        public int MaxDelaySeconds { get; set; } = 30;

        /// <summary>
        /// How long a failure streak is remembered with no further attempts before it resets to zero (the
        /// state's sliding TTL). Always at least the active delay, so an in-effect lock is never lost early.
        /// </summary>
        public int FailureWindowSeconds { get; set; } = 900;
    }
}
