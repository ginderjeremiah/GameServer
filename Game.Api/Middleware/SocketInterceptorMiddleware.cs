using Game.Api.Services;
using Game.Application.Services;

namespace Game.Api.Middleware
{
    public class SocketInterceptorMiddleware(RequestDelegate next)
    {
        private readonly RequestDelegate _next = next;

        /// <summary>
        /// The application request pipeline hook.
        /// </summary>
        public async Task InvokeAsync(HttpContext context, SessionService sessionService, SocketManagerService socketManager, IServiceScopeFactory scopeFactory)
        {
            if (context.Request.Path != "/socket")
            {
                await _next(context);
            }
            else if (!sessionService.Authenticated)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            }
            else if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
            }
            else if (!await TryLoadPlayer(sessionService, scopeFactory))
            {
                // An authenticated session whose player can't be loaded can't play, so fail before
                // upgrading the socket rather than erroring on the first command.
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            }
            else
            {
                using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                var socketContext = await socketManager.RegisterSocket(webSocket, sessionService);
                await socketContext.WaitSocketClosed();
                await socketManager.UnRegisterSocket(socketContext);
            }
        }

        /// <summary>
        /// Loads the player aggregate up front into the session so socket commands read it synchronously for
        /// the connection's lifetime (the connection never re-reads the cache per command). The load runs in a
        /// disposable scope so its <c>GameContext</c> is not held open for the whole connection. Returns false
        /// when the authenticated player has no loadable aggregate.
        /// </summary>
        private static async Task<bool> TryLoadPlayer(SessionService sessionService, IServiceScopeFactory scopeFactory)
        {
            using var scope = scopeFactory.CreateScope();
            var player = await scope.ServiceProvider.GetRequiredService<PlayerService>()
                .LoadPlayer(sessionService.SelectedPlayerId);
            if (player is null)
            {
                return false;
            }

            sessionService.SetPlayer(player);
            return true;
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
            SocketCommandFactory.RegisterSocketCommandGenerators();
            return builder.UseMiddleware<SocketInterceptorMiddleware>();
        }
    }
}
