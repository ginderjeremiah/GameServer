using Game.Api.Services;
using Game.Application.Services;
using Microsoft.IdentityModel.JsonWebTokens;
using System.Security.Claims;

namespace Game.Api.Middleware
{
    /// <summary>
    /// Populates the request-scoped <see cref="SessionService"/> from the authenticated
    /// <see cref="ClaimsPrincipal"/> produced by JWT bearer authentication. Authentication and
    /// authorization themselves are handled by the standard ASP.NET Core middleware; this only bridges
    /// the validated token's user id to the player session. The session is loaded from the cache, and a
    /// miss for a still-valid token is rehydrated from the database rather than treated as anonymous.
    /// </summary>
    public class SessionLoaderMiddleware(RequestDelegate next)
    {
        private readonly RequestDelegate _next = next;

        public async Task InvokeAsync(
            HttpContext context,
            SessionService sessionService,
            AccountService accountService,
            ILogger<SessionLoaderMiddleware> logger)
        {
            var principal = context.User;
            if (principal.Identity?.IsAuthenticated == true
                && int.TryParse(principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var userId))
            {
                await sessionService.LoadPlayerState(userId, context.RequestAborted);

                // A valid token whose session cache entry is gone (Redis flush, TTL lapse, or a session
                // never established on this instance) is authenticated-but-uncached, not anonymous. Rebuild
                // the session from the user's player binding so the request proceeds, instead of being
                // reported as "not logged in".
                if (!sessionService.HasPlayerSession)
                {
                    await RehydrateSession(sessionService, accountService, logger, userId);
                }
            }

            await _next(context);
        }

        private static async Task RehydrateSession(
            SessionService sessionService,
            AccountService accountService,
            ILogger<SessionLoaderMiddleware> logger,
            int userId)
        {
            var playerId = await accountService.ResolveSelectedPlayerId(userId);
            if (playerId is null)
            {
                logger.LogWarning(
                    "Authenticated user {UserId} has a valid token but no resolvable player; session not established.",
                    userId);
                return;
            }

            logger.LogInformation("Rehydrating evicted session for authenticated user {UserId}.", userId);
            sessionService.RehydrateSession(playerId.Value);
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
