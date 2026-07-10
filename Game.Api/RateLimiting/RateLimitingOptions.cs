namespace Game.Api.RateLimiting
{
    /// <summary>
    /// Configuration for the auth-endpoint rate limiter, bound from the "RateLimiting" configuration
    /// section. Unlike the CORS origins, these carry safe non-empty defaults so an unconfigured deployment
    /// is still protected (security hardening should be on by default); a deployment only overrides them to
    /// tune the limits. The values are validated as positive on start so an accidental zero fails fast
    /// rather than silently disabling protection.
    /// </summary>
    public class RateLimitingOptions
    {
        /// <summary>The configuration section this options class binds from.</summary>
        public const string SectionName = "RateLimiting";

        /// <summary>The named policy applied to the anonymous auth endpoints.</summary>
        public const string AuthPolicy = "auth";

        /// <summary>
        /// The per-client-IP window for the anonymous auth endpoints (login, refresh, account creation, logout) —
        /// the credential-stuffing, refresh-token brute-force, and PBKDF2 resource-exhaustion surface.
        /// </summary>
        public RateLimitWindow Auth { get; set; } = new() { PermitLimit = 10, WindowSeconds = 60 };
    }

    /// <summary>A fixed-window rate limit: at most <see cref="PermitLimit"/> requests per window.</summary>
    public class RateLimitWindow
    {
        /// <summary>Maximum number of requests permitted per window per partition.</summary>
        public int PermitLimit { get; set; }

        /// <summary>The length of the fixed window, in seconds.</summary>
        public int WindowSeconds { get; set; }
    }
}
