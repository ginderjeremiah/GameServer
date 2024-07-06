using GameCore;
using GameServer.Services;
using System.Net.WebSockets;

namespace GameServer.Sockets
{
    public class SocketInterceptorMiddleware(RequestDelegate next)
    {
        private readonly RequestDelegate _next = next;

        /// <summary>
        /// The application request pipeline hook.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="logger"></param>
        /// <param name="sessionService"></param>
        /// <param name="socketManager"></param>
        /// <param name="commandFactory"></param>
        /// <returns></returns>
        public async Task InvokeAsync(HttpContext context, IApiLogger logger, SessionService sessionService, SocketManagerService socketManager, SocketCommandFactory commandFactory)
        {
            if (context.Request.Path == "/EstablishSocket")
            {
                if (sessionService.SessionAvailable)
                {
                    if (context.WebSockets.IsWebSocketRequest)
                    {
                        var player = sessionService.GetSession().Player;
                        using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                        var socketHandler = new SocketHandler(webSocket, commandFactory, logger, player.Id);
                        await socketManager.RegisterSocket(socketHandler);
                        logger.LogDebug($"Initiated socket for player: {player.UserName} ({player.Id}), with Id: {socketHandler.Id}");

                        var closeReason = await socketHandler.SocketFinished.Task;
                        await socketManager.UnRegisterSocket(socketHandler);
                        if (webSocket.State is WebSocketState.Open)
                        {
                            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, closeReason.GetDescription(), CancellationToken.None);
                            logger.LogDebug($"Closing socket for player: {player.UserName}, {player.Id}, with Id: {socketHandler.Id}");
                        }

                        logger.LogDebug($"{nameof(SocketInterceptorMiddleware)} complete for: {player.UserName}, {player.Id}");
                    }
                    else
                    {
                        context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    }
                }
                else
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                }
            }
            else
            {
                await _next(context);
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
