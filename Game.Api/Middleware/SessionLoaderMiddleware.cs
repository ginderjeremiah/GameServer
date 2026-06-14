using Game.Api.Services;
using Microsoft.IdentityModel.JsonWebTokens;
using System.Security.Claims;

namespace Game.Api.Middleware
{
    /// <summary>
    /// Populates the request-scoped <see cref="SessionService"/> from the authenticated
    /// <see cref="ClaimsPrincipal"/> produced by JWT bearer authentication. Authentication and
    /// authorization themselves are handled by the standard ASP.NET Core middleware; this only bridges
    /// the validated token's user id to the player session loaded from the cache.
    /// </summary>
    public class SessionLoaderMiddleware(RequestDelegate next)
    {
        private readonly RequestDelegate _next = next;

        public async Task InvokeAsync(HttpContext context, SessionService sessionService)
        {
            var principal = context.User;
            if (principal.Identity?.IsAuthenticated == true
                && int.TryParse(principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var userId))
            {
                await sessionService.LoadPlayerState(userId);
            }

            await _next(context);
        }
    }

    /// <summary>
    /// A class to facilitate adding the <see cref="SessionLoaderMiddleware"/> to the <see cref="IApplicationBuilder"/>
    /// </summary>
    public static class SessionLoaderMiddlewareExtensions
    {
        /// <summary>
        /// Adds middleware that initializes a <see cref="SessionService"/> from the authenticated principal.
        /// Must run after <c>UseAuthentication</c> so the principal is available.
        /// </summary>
        public static IApplicationBuilder UseSessionLoader(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<SessionLoaderMiddleware>();
        }
    }
}
