using Game.Api.Services;

namespace Game.Api.Middleware
{
    public class RequestLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestLoggingMiddleware> _logger;

        public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, SessionService sessionService)
        {
            var loggingContext = new object(); //TODO: load some useful context data into this object;
            using var scope = _logger.BeginScope(loggingContext);
            _logger.LogInformation("Request Start");
            await _next(context);
            _logger.LogInformation("Request Ended");
        }
    }
}
