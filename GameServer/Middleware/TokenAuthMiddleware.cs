﻿using GameCore;
using GameServer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using System.Diagnostics;

namespace GameServer.Middleware
{
    /// <summary>
    /// Middleware to intialize a <see cref="SessionService"/> using authorization info in the request.
    /// </summary>
    /// <param name="next">The next action in the request pipeline.</param>
    public class TokenAuthMiddleware(RequestDelegate next)
    {
        private readonly RequestDelegate _next = next;

        /// <summary>
        /// The application request pipeline hook.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="logger"></param>
        /// <param name="sessionService"></param>
        /// <param name="cookieService"></param>
        /// <returns></returns>
        public async Task InvokeAsync(HttpContext context, IApiLogger logger, SessionService sessionService, CookieService cookieService)
        {
            long startTime = Stopwatch.GetTimestamp();
            logger.LogDebug("Starting TokenAuth.");

            await sessionService.LoadSession();

            if (sessionService.SessionAvailable)
            {
                logger.LogDebug($"Succeeded TokenAuth: {Stopwatch.GetElapsedTime(startTime).TotalMilliseconds} ms");
            }
            else
            {
                //TODO: Move allowAnonymous to database/redis call
                var allowAnonymous = false;
                var endpoint = context.GetEndpoint();
                if (endpoint is not null)
                {
                    var actionDescriptor = endpoint.Metadata.GetMetadata<ControllerActionDescriptor>();
                    if (actionDescriptor is not null)
                    {
                        allowAnonymous = actionDescriptor.MethodInfo.GetCustomAttributes(true).Any(f => f is AllowAnonymousAttribute);
                    }
                }

                if (!allowAnonymous)
                {
                    logger.LogError($"Failed TokenAuth: {Stopwatch.GetElapsedTime(startTime).TotalMilliseconds} ms");
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return;
                }
                else
                {
                    logger.LogInfo($"Allowed Anonymous TokenAuth: {Stopwatch.GetElapsedTime(startTime).TotalMilliseconds} ms");
                }
            }

            await _next(context);
        }
    }

    /// <summary>
    /// A class to facilitate adding the <see cref="TokenAuthMiddleware"/> to the <see cref="IApplicationBuilder"/>
    /// </summary>
    public static class TokenAuthMiddlewareExtensions
    {
        /// <summary>
        /// Adds middleware for authorizing requests and initializing a <see cref="SessionService"/>.
        /// </summary>
        /// <param name="builder">The <see cref="IApplicationBuilder"/> instance this method extends.</param>
        /// <returns>The <see cref="IApplicationBuilder"/> with token authorization for <see cref="SessionService"/>.</returns>
        public static IApplicationBuilder UseTokenAuth(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<TokenAuthMiddleware>();
        }
    }
}
