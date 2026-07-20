using Game.Api.Models.Common;
using Game.Api.Services;
using Game.Application.Services;
using Game.Core;

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
            else
            {
                var loadResult = await TryLoadPlayer(sessionService, sessionInitializer, scopeFactory, context.RequestAborted);
                if (loadResult == PlayerLoadResult.NoPlayerSelected)
                {
                    // A pre-selection token (post-Login, pre-SelectPlayer) can never resolve a loadable
                    // player, so reject on the token's missing selected-player claim with the same category
                    // AuthController.Status uses, rather than let a well-behaved-client-can't-hit-this state
                    // fall through to the generic "player not found" 404 below.
                    context.Response.StatusCode = StatusCodes.Status409Conflict;
                    await context.Response.WriteAsJsonAsync(ApiResponse.Error("No character selected.", ApiErrorCategory.NoPlayerSelected), context.RequestAborted);
                }
                else if (loadResult == PlayerLoadResult.PlayerNotFound)
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
                    // Read from the handshake's validated ClaimsPrincipal (the same source
                    // AdminRoleAuthorizationFilter reads for HTTP admin endpoints), not the volatile session
                    // cache, so it derives from the token alone.
                    var isAdmin = context.User.IsInRole(nameof(ERole.Admin));
                    // The client offers the access token as the sole requested subprotocol (see Startup's
                    // JwtBearerEvents.OnMessageReceived); the handshake must echo one back or browsers fail
                    // the connection outright when a subprotocol list was offered but none was selected.
                    var requestedProtocols = context.WebSockets.WebSocketRequestedProtocols;
                    var subProtocol = requestedProtocols.Count > 0 ? requestedProtocols[0] : null;
                    using var webSocket = await context.WebSockets.AcceptWebSocketAsync(subProtocol);
                    var socketContext = await socketManager.RegisterSocket(webSocket, sessionService, isAdmin);
                    await socketContext.WaitSocketClosed();
                    await socketManager.UnRegisterSocket(socketContext);
                }
            }
        }

        /// <summary>Whether (and why not) the handshake was able to load a player aggregate for the session.</summary>
        private enum PlayerLoadResult
        {
            Loaded,
            NoPlayerSelected,
            PlayerNotFound
        }

        /// <summary>
        /// Establishes the player session for the connection: first loads (or rehydrates) the in-flight
        /// player state — the HTTP pipeline no longer does this per request, so the socket handshake is where
        /// it happens — then loads the player aggregate up front so socket commands read it synchronously for
        /// the connection's lifetime (the connection never re-reads the cache per command). The aggregate load
        /// runs in a disposable scope so its <c>GameContext</c> is not held open for the whole connection.
        /// Rejects a pre-selection token before any load I/O, since <see cref="SessionService.SelectedPlayerId"/>
        /// can never resolve a real player while it's unbound (player identity starts at 1).
        /// </summary>
        private static async Task<PlayerLoadResult> TryLoadPlayer(SessionService sessionService, SessionInitializer sessionInitializer, IServiceScopeFactory scopeFactory, CancellationToken cancellationToken)
        {
            await sessionInitializer.EnsureSessionLoaded(cancellationToken);

            if (sessionService.TokenSelectedPlayerId is null)
            {
                return PlayerLoadResult.NoPlayerSelected;
            }

            using var scope = scopeFactory.CreateScope();
            var player = await scope.ServiceProvider.GetRequiredService<PlayerService>()
                .LoadPlayer(sessionService.SelectedPlayerId, cancellationToken);
            if (player is null)
            {
                return PlayerLoadResult.PlayerNotFound;
            }

            sessionService.SetPlayer(player);
            return PlayerLoadResult.Loaded;
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
