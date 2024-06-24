using GameCore;
using GameServer.Services;
using Microsoft.AspNetCore.Mvc.Controllers;
using System.Diagnostics;

namespace GameServer.Auth
{
    public class SessionAuthMiddleware(RequestDelegate next)
    {
        private readonly RequestDelegate _next = next;

        public async Task InvokeAsync(HttpContext context, IApiLogger logger, SessionService sessionService)
        {
            long startTime = Stopwatch.GetTimestamp();
            logger.LogDebug("Starting SessionAuth.");

            var allowAll = false;
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

            if (tokenParts != null && tokenParts.Length == 3 && long.TryParse(tokenParts[1].FromBase64(), out var ticks) && ticks < now.Ticks)
            {
                var session = await sessionService.LoadSession(tokenParts[0].FromBase64());
                if (session is null || tokenParts[2].FromBase64() != $"{tokenParts[0]}.{tokenParts[1]}".Hash(session.Player.Salt.ToString(), 1))
                {
                    if (!allowAll)
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    }

                    logger.LogError($"Failed SessionAuth: {Stopwatch.GetElapsedTime(startTime).TotalMilliseconds} ms");
                    return;
                }

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

            logger.LogDebug($"Succeeded SessionAuth: {Stopwatch.GetElapsedTime(startTime).TotalMilliseconds} ms");

            await _next(context);
        }
    }

    public static class SessionAuthMiddlewareExtensions
    {
        public static IApplicationBuilder UseSessionAuth(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<SessionAuthMiddleware>();
        }
    }
}
