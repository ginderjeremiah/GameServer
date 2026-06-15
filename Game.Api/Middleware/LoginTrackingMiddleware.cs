using Game.Api.Http;
using Game.Api.Services;
using Game.Application.Services;

namespace Game.Api.Middleware
{
    /// <summary>
    /// Records the connection (user, IP, device) of each authenticated standard HTTP request, refreshing
    /// the user's last-connection timestamp. Runs after <see cref="SessionLoaderMiddleware"/> so the
    /// request's user identity is available. Recording is keyed on the device fingerprint the frontend
    /// sends as a header, so a request without one (e.g. a WebSocket handshake, which cannot set custom
    /// headers) is skipped. Tracking is best-effort: any failure is logged and swallowed so it never
    /// affects the response.
    /// </summary>
    public class LoginTrackingMiddleware(RequestDelegate next, ILogger<LoginTrackingMiddleware> logger)
    {
        private readonly RequestDelegate _next = next;
        private readonly ILogger<LoginTrackingMiddleware> _logger = logger;

        public async Task InvokeAsync(HttpContext context, SessionService sessionService, LoginTrackingService trackingService)
        {
            var fingerprint = ClientHints.DeviceFingerprint(context.Request.Headers);
            if (sessionService.Authenticated && fingerprint is not null)
            {
                try
                {
                    var hints = ClientHints.FromHeaders(context.Request.Headers);
                    await trackingService.RecordConnection(
                        sessionService.UserId,
                        ResolveIpAddress(context),
                        fingerprint,
                        hints.UserAgent,
                        hints.SecChUa,
                        hints.SecChUaMobile,
                        hints.SecChUaPlatform);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to record login tracking for user {UserId}.", sessionService.UserId);
                }
            }

            await _next(context);
        }

        /// <summary>
        /// Resolves the originating client IP, preferring the first <c>X-Forwarded-For</c> entry when the
        /// request arrives through a reverse proxy, and falling back to the transport remote address.
        /// </summary>
        internal static string ResolveIpAddress(HttpContext context)
        {
            var forwardedFor = context.Request.Headers["X-Forwarded-For"].ToString();
            if (!string.IsNullOrWhiteSpace(forwardedFor))
            {
                return forwardedFor.Split(',')[0].Trim();
            }

            return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }
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
