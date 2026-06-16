using Game.Api.Models.Common;

namespace Game.Api.Middleware
{
    /// <summary>
    /// Catches unhandled exceptions from the downstream pipeline, logs each one exactly once, and writes a
    /// consistent <see cref="ApiResponse"/> error envelope so clients receive the project's structured error
    /// shape on a 500 instead of the host's default response. It is the single place that logs unhandled
    /// exceptions, keeping <see cref="RequestLoggingMiddleware"/> focused on structured start/end events.
    /// </summary>
    public class ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        private readonly RequestDelegate _next = next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger = logger;
        private readonly IHostEnvironment _environment = environment;

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                // The client disconnected mid-request, so there is no one to respond to and the connection
                // is gone. This is not a server error worth a 500 or an error-level log.
                _logger.LogDebug("Request {Method} {Path} aborted by the client.",
                    context.Request.Method, context.Request.Path.Value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception processing {Method} {Path}.",
                    context.Request.Method, context.Request.Path.Value);

                if (context.Response.HasStarted)
                {
                    // The response is already (partly) on the wire — e.g. an upgraded WebSocket — so it
                    // cannot be replaced with the error envelope. Let it unwind to the host.
                    throw;
                }

                context.Response.Clear();
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                // Internal details (message, stack trace) are only safe to surface in Development; other
                // environments get a generic message so nothing about the failure leaks to the client.
                var message = _environment.IsDevelopment() ? ex.Message : "Internal Server Error";
                await context.Response.WriteAsJsonAsync(ApiResponse.Error(message), context.RequestAborted);
            }
        }
    }

    /// <summary>
    /// Extension methods for adding <see cref="ExceptionHandlingMiddleware"/> to the application pipeline.
    /// </summary>
    public static class ExceptionHandlingMiddlewareExtensions
    {
        /// <summary>
        /// Adds middleware that converts unhandled exceptions into the project's consistent
        /// <see cref="ApiResponse"/> error envelope and logs them exactly once. Must run inside
        /// <c>UseRequestLogging</c> so the error status is set before the request-ended event is logged.
        /// </summary>
        public static IApplicationBuilder UseExceptionHandling(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<ExceptionHandlingMiddleware>();
        }
    }
}
