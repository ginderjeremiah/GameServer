using GameCore;
using GameServer.Auth;
using GameServer.Services;
using Microsoft.AspNetCore.Mvc.Controllers;
using System.Diagnostics;

namespace GameServer.Middleware
{
    /// <summary>
    /// Middleware to enable initializing a <see cref="SessionService"/> using authorization info in the request.
    /// </summary>
    /// <param name="next">The next action in the request pipeline.</param>
    public class SessionAuthMiddleware(RequestDelegate next)
    {
        private readonly RequestDelegate _next = next;

        /// <summary>
        /// The application request pipeline hook.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="logger"></param>
        /// <param name="sessionService"></param>
        /// <returns></returns>
        public async Task InvokeAsync(HttpContext context, IApiLogger logger, SessionService sessionService)
        {
            long startTime = Stopwatch.GetTimestamp();
            logger.LogDebug("Starting SessionAuth.");

            var allowAll = true;
            var endpoint = context.GetEndpoint();
            if (endpoint is not null)
            {
                var actionDescriptor = endpoint.Metadata.GetMetadata<ControllerActionDescriptor>();
                if (actionDescriptor is not null)
                {
                    var filter = actionDescriptor.FilterDescriptors.Select(fd => fd.Filter).FirstOrDefault(f => f is SessionAuthorizeAttribute);
                    if (filter is not null)
                    {
                        allowAll = ((SessionAuthorizeAttribute)filter).AllowAll;
                    }
                }
            }

            var token = context.Request.Cookies[Constants.TOKEN_NAME];
            var tokenParts = token?.Split('.');
            var now = DateTime.UtcNow;

            if (tokenParts != null && tokenParts.Length == 3 && long.TryParse(tokenParts[1].FromBase64(), out var ticks) && ticks >= now.Ticks)
            {
                var session = await sessionService.LoadSession(tokenParts[0].FromBase64());
                if (session is not null && tokenParts[2].FromBase64() == $"{tokenParts[0]}.{tokenParts[1]}".Hash(session.Player.Salt.ToString(), 1))
                {
                    //Slide cookie if over halfway to expiration
                    if (ticks < now.Add(Constants.TOKEN_LIFETIME / 2).Ticks)
                    {
                        context.Response.Cookies.Append(Constants.TOKEN_NAME, session.GetNewToken(), new CookieOptions()
                        {
                            Secure = true,
                            HttpOnly = true,
                            Expires = now.Add(Constants.TOKEN_LIFETIME)
                        });
                    }
                }
            }

            if (sessionService.SessionAvailable)
            {
                logger.LogDebug($"Succeeded SessionAuth: {Stopwatch.GetElapsedTime(startTime).TotalMilliseconds} ms");
            }
            else
            {
                logger.LogError($"Failed SessionAuth: {Stopwatch.GetElapsedTime(startTime).TotalMilliseconds} ms");
                if (!allowAll)
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return;
                }
            }

            await _next(context);
        }
    }

    /// <summary>
    /// A class to facilitate adding the <see cref="SessionAuthMiddleware"/> to the <see cref="IApplicationBuilder"/>
    /// </summary>
    public static class SessionAuthMiddlewareExtensions
    {
        /// <summary>
        /// Adds middleware for authorizing requests and initializing a <see cref="SessionService"/>.
        /// </summary>
        /// <param name="builder">The <see cref="IApplicationBuilder"/> instance this method extends.</param>
        /// <returns>The <see cref="IApplicationBuilder"/> with session authorization for <see cref="SessionService"/>.</returns>
        public static IApplicationBuilder UseSessionAuth(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<SessionAuthMiddleware>();
        }
    }
}
