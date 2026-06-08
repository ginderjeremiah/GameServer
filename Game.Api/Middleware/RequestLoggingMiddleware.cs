using Game.Api.Services;
using System.Diagnostics;

namespace Game.Api.Middleware
{
    /// <summary>
    /// Logs structured request start and end events for each HTTP request, including the HTTP method,
    /// path, requesting user, response status code, and elapsed time.
    /// </summary>
    public class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        private readonly RequestDelegate _next = next;
        private readonly ILogger<RequestLoggingMiddleware> _logger = logger;

        public async Task InvokeAsync(HttpContext context, SessionService sessionService)
        {
            var loggingContext = new Dictionary<string, object?>
            {
                ["Method"] = context.Request.Method,
                ["Path"] = context.Request.Path.Value,
                ["RequestId"] = context.TraceIdentifier,
                ["UserId"] = sessionService.Authenticated ? sessionService.UserId : (int?)null
            };

            var stopwatch = Stopwatch.StartNew();
            using var scope = _logger.BeginScope(loggingContext);
            _logger.LogInformation("Request Start");

            await _next(context);

            stopwatch.Stop();
            _logger.LogInformation("Request Ended {StatusCode} {ElapsedMs}ms",
                context.Response.StatusCode, stopwatch.ElapsedMilliseconds);
        }
    }

    /// <summary>
    /// Extension methods for adding <see cref="RequestLoggingMiddleware"/> to the application pipeline.
    /// </summary>
    public static class RequestLoggingMiddlewareExtensions
    {
        /// <summary>
        /// Adds middleware that logs structured request start and end events. Must run after
        /// <c>UseSessionLoader</c> so the requesting user's identity is available.
        /// </summary>
        public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<RequestLoggingMiddleware>();
        }
    }
}
