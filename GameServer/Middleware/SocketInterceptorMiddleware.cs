using GameServer.Services;

namespace GameServer.Middleware
{
    public class SocketInterceptorMiddleware(RequestDelegate next)
    {
        private readonly RequestDelegate _next = next;

        /// <summary>
        /// The application request pipeline hook.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="sessionService"></param>
        /// <param name="socketManager"></param>
        /// <returns></returns>
        public async Task InvokeAsync(HttpContext context, SessionService sessionService, SocketManagerService socketManager)
        {
            if (context.Request.Path != "/EstablishSocket")
            {
                await _next(context);
            }
            else if (!sessionService.SessionAvailable)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            }
            else if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
            }
            else
            {
                var player = sessionService.GetSession().Player;
                using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                var socketContext = await socketManager.RegisterSocket(webSocket, player);
                await socketContext.WaitSocketClosed();
                await socketManager.UnRegisterSocket(socketContext);
            }
        }
    }

    /// <summary>
    /// A class to facilitate adding the <see cref="SocketInterceptorMiddleware"/> to the <see cref="IApplicationBuilder"/>
    /// </summary>
    public static class SocketInterceptorMiddlewareExtensions
    {
        /// <summary>
        /// Adds middleware for intercepting socket requests and establishing a socket connection.
        /// </summary>
        /// <param name="builder">The <see cref="IApplicationBuilder"/> instance this method extends.</param>
        /// <returns>The <see cref="IApplicationBuilder"/> with an interceptor for socket requests.</returns>
        public static IApplicationBuilder UseSocketInterceptor(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<SocketInterceptorMiddleware>();
        }
    }
}
