using Game.Api.Services;
using Microsoft.IdentityModel.JsonWebTokens;
using System.Security.Claims;

namespace Game.Api.Middleware
{
    /// <summary>
    /// Records the authenticated user on the request-scoped <see cref="SessionService"/> from the
    /// <see cref="ClaimsPrincipal"/> produced by JWT bearer authentication, so the user id (the sole
    /// authority for <see cref="SessionService.Authenticated"/>) is available to every downstream consumer.
    /// Loading the player session itself is deferred to the consumers that actually read player state (see
    /// <see cref="SessionInitializer"/>), so a request that never touches player state pays no per-request
    /// session-cache read.
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
                sessionService.SetAuthenticatedUser(userId);
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
        /// Adds middleware that records the authenticated user on a <see cref="SessionService"/> from the
        /// validated principal. Must run after <c>UseAuthentication</c> so the principal is available.
        /// </summary>
        public static IApplicationBuilder UseSessionLoader(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<SessionLoaderMiddleware>();
        }
    }
}
