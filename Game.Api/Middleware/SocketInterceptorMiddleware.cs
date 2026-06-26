using Game.Api.Models.Common;
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
        public async Task InvokeAsync(HttpContext context, SessionService sessionService, SessionInitializer sessionInitializer, SocketManagerService socketManager, IServiceScopeFactory scopeFactory)
        {
            if (context.Request.Path != "/socket")
            {
                await _next(context);
            }
            else if (!sessionService.Authenticated)
            {
                // The socket isn't upgraded yet, so a body can still be written: return the project's
                // { errorMessage } envelope rather than a bare status code.
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(ApiResponse.Error("Authentication is required to open a socket connection."), context.RequestAborted);
            }
            else if (!context.WebSockets.IsWebSocketRequest)
            {
                // As above, a body is still writable before the (here, absent) upgrade.
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(ApiResponse.Error("The /socket endpoint requires a WebSocket upgrade request."), context.RequestAborted);
            }
            else if (!await TryLoadPlayer(sessionService, sessionInitializer, scopeFactory, context.RequestAborted))
            {
                // An authenticated session whose player can't be loaded can't play, so fail before upgrading
                // the socket rather than erroring on the first command. The socket isn't upgraded yet, so a
                // body can still be written: return the project's { errorMessage } envelope (a 404 — the
                // player resource is absent, not a server fault; a genuine load error throws and is shaped by
                // ExceptionHandlingMiddleware) rather than a bare 500.
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                await context.Response.WriteAsJsonAsync(ApiResponse.Error("Player could not be loaded."), context.RequestAborted);
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
        /// Establishes the player session for the connection: first loads (or rehydrates) the in-flight
        /// player state — the HTTP pipeline no longer does this per request, so the socket handshake is where
        /// it happens — then loads the player aggregate up front so socket commands read it synchronously for
        /// the connection's lifetime (the connection never re-reads the cache per command). The aggregate load
        /// runs in a disposable scope so its <c>GameContext</c> is not held open for the whole connection.
        /// Returns false when the authenticated player has no loadable aggregate.
        /// </summary>
        private static async Task<bool> TryLoadPlayer(SessionService sessionService, SessionInitializer sessionInitializer, IServiceScopeFactory scopeFactory, CancellationToken cancellationToken)
        {
            await sessionInitializer.EnsureSessionLoaded(cancellationToken);

            using var scope = scopeFactory.CreateScope();
            var player = await scope.ServiceProvider.GetRequiredService<PlayerService>()
                .LoadPlayer(sessionService.SelectedPlayerId, cancellationToken);
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
