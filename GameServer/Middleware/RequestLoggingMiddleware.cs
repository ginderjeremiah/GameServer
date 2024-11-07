using GameServer.Services;

namespace GameServer.Middleware
{
    public class RequestLoggingMiddleware
    {

        public async Task InvokeAsync(HttpContext context, ILogger<RequestLoggingMiddleware> logger, SessionService sessionService)
        {
        }
    }
}
