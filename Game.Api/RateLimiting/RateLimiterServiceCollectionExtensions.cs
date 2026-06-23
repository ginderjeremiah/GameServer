using Game.Api.Http;
using Game.Api.Models.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Threading.RateLimiting;

namespace Game.Api.RateLimiting
{
    /// <summary>
    /// Registers the auth-endpoint rate limiter: a per-client-IP fixed-window policy that throttles the
    /// anonymous auth endpoints to blunt credential stuffing, refresh-token brute force, and the PBKDF2
    /// resource-exhaustion vector (#950). The partition key is resolved the same trusted-proxy way as the
    /// login-IP tracking, so a spoofed <c>X-Forwarded-For</c> can't shard an attacker across partitions.
    /// </summary>
    public static class RateLimiterServiceCollectionExtensions
    {
        public static IServiceCollection AddAuthRateLimiter(this IServiceCollection services)
        {
            services.AddOptions<RateLimitingOptions>()
                .BindConfiguration(RateLimitingOptions.SectionName)
                .Validate(
                    options => options.Auth.PermitLimit > 0 && options.Auth.WindowSeconds > 0,
                    "RateLimiting:Auth PermitLimit and WindowSeconds must be positive")
                .ValidateOnStart();

            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.OnRejected = OnRejected;
                options.AddPolicy(RateLimitingOptions.AuthPolicy, BuildAuthPartition);
            });

            return services;
        }

        // One fixed-window partition per client IP. A new partition's limits are read once (on first touch)
        // from the validated options, so a deployment's configured limits apply without re-reading per call.
        private static RateLimitPartition<string> BuildAuthPartition(HttpContext httpContext)
        {
            var limits = httpContext.RequestServices
                .GetRequiredService<IOptions<RateLimitingOptions>>().Value.Auth;

            return RateLimitPartition.GetFixedWindowLimiter(ClientIp.Resolve(httpContext), _ =>
                new FixedWindowRateLimiterOptions
                {
                    PermitLimit = limits.PermitLimit,
                    Window = TimeSpan.FromSeconds(limits.WindowSeconds),
                    QueueLimit = 0,
                });
        }

        // Surface the throttle as the project's standard { errorMessage } envelope (mirroring every other
        // failure response) plus a Retry-After hint, rather than the framework's empty 429 body.
        private static async ValueTask OnRejected(OnRejectedContext context, CancellationToken token)
        {
            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

            if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            {
                // Round up (matching the per-account login backoff's Retry-After): truncating a sub-second
                // remaining window to 0 would tell the client it may retry immediately when it can't.
                context.HttpContext.Response.Headers.RetryAfter =
                    ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
            }

            await context.HttpContext.Response.WriteAsJsonAsync(
                new ApiResponse { ErrorMessage = "Too many requests. Please slow down and try again shortly." },
                token);
        }
    }
}
