using Game.Api.Http;
using Game.Api.Services;
using Game.Application.Services;
using Microsoft.Extensions.Caching.Memory;

namespace Game.Api.Middleware
{
    /// <summary>
    /// Records the connection (user, IP, device) of each authenticated standard HTTP request, refreshing
    /// the user's last-connection timestamp. Runs after <see cref="SessionLoaderMiddleware"/> so the
    /// request's user identity is available. Recording is keyed on the device fingerprint the frontend
    /// sends as a header, so a request without one (e.g. a WebSocket handshake, which cannot set custom
    /// headers) is skipped.
    /// <para>
    /// Repeat requests from the same (user, IP, device) within <see cref="DedupeWindow"/> are skipped via
    /// an in-memory, instance-local memo — the connection row is idempotent, so re-recording it adds no
    /// information but costs a multi-query upsert every time. This keeps a short burst of requests from
    /// the same device (the login flow's several hops, a run of admin Workbench saves) from each paying
    /// the DB round-trip. The memo is only set after a successful save, so a failed attempt is retried on
    /// the next request rather than being silently memoized as done; a genuinely new device/IP is still
    /// recorded promptly since it always misses the memo on its first request.
    /// </para>
    /// <para>
    /// Tracking is best-effort and <b>structurally isolated</b>: it runs on its own DI scope (its own
    /// <c>GameContext</c>) and self-commits there, so a tracking save failure — which the swallow below
    /// hides — can neither ride along on nor break the request's own unit of work (the per-action
    /// <see cref="Filters.CommitFilter"/> commits a different context). Any failure is logged and
    /// swallowed so it never affects the response, and the scope is disposed regardless of how the save
    /// ended, discarding anything it queued.
    /// </para>
    /// </summary>
    public class LoginTrackingMiddleware(RequestDelegate next, ILogger<LoginTrackingMiddleware> logger)
    {
        private static readonly TimeSpan DedupeWindow = TimeSpan.FromMinutes(5);

        private readonly RequestDelegate _next = next;
        private readonly ILogger<LoginTrackingMiddleware> _logger = logger;

        public async Task InvokeAsync(HttpContext context, SessionService sessionService, IServiceScopeFactory scopeFactory, IMemoryCache cache)
        {
            var cacheKey = ResolvePendingTracking(context, sessionService, cache);

            // Tracking runs after the request handler (in a finally, so it still fires if the handler
            // throws) rather than before it. It's best-effort telemetry the response never depends on, so
            // it shouldn't sit on the endpoint's latency path — and during a DB outage, it shouldn't force
            // every authenticated request through a tracking timeout before the endpoint (which may not
            // even touch Postgres) gets a chance to run.
            try
            {
                await _next(context);
            }
            finally
            {
                if (cacheKey is not null)
                {
                    await RecordConnectionAsync(context, scopeFactory, cache, cacheKey.Value);
                }
            }
        }

        private static TrackingCacheKey? ResolvePendingTracking(HttpContext context, SessionService sessionService, IMemoryCache cache)
        {
            var fingerprint = ClientHints.DeviceFingerprint(context.Request.Headers);
            if (!sessionService.Authenticated || fingerprint is null)
            {
                return null;
            }

            var cacheKey = new TrackingCacheKey(sessionService.UserId, ClientIp.Resolve(context), fingerprint);
            return cache.TryGetValue<bool>(cacheKey, out _) ? null : cacheKey;
        }

        private async Task RecordConnectionAsync(
            HttpContext context,
            IServiceScopeFactory scopeFactory,
            IMemoryCache cache,
            TrackingCacheKey cacheKey)
        {
            try
            {
                var hints = ClientHints.FromHeaders(context.Request.Headers);
                // Resolve tracking from a dedicated scope so its GameContext is independent of the
                // request's. RecordConnection self-commits; sharing the request context would let a
                // non-unique save failure leave queued inserts that the later per-request commit
                // re-flushes (or breaks on). The scope is disposed here whatever the outcome.
                await using var scope = scopeFactory.CreateAsyncScope();
                var trackingService = scope.ServiceProvider.GetRequiredService<LoginTrackingService>();
                await trackingService.RecordConnection(
                    cacheKey.UserId,
                    cacheKey.IpAddress,
                    cacheKey.Fingerprint,
                    hints.UserAgent,
                    hints.SecChUa,
                    hints.SecChUaMobile,
                    hints.SecChUaPlatform,
                    context.RequestAborted);

                cache.Set(cacheKey, true, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = DedupeWindow,
                    Size = 1,
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to record login tracking for user {UserId}.", cacheKey.UserId);
            }
        }

        private readonly record struct TrackingCacheKey(int UserId, string IpAddress, string Fingerprint);
    }

    /// <summary>
    /// A class to facilitate adding the <see cref="LoginTrackingMiddleware"/> to the <see cref="IApplicationBuilder"/>
    /// </summary>
    public static class LoginTrackingMiddlewareExtensions
    {
        /// <summary>
        /// Adds middleware that records connection tracking for authenticated requests. Must run after
        /// <c>UseSessionLoader</c> so the request's user identity is populated.
        /// </summary>
        public static IApplicationBuilder UseLoginTracking(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<LoginTrackingMiddleware>();
        }
    }
}
