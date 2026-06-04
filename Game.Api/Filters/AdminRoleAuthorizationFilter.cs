using Game.Api.Services;
using Game.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Game.Api.Filters
{
    /// <summary>
    /// Authorization filter that restricts an endpoint to users granted the <see cref="ERole.Admin"/> role.
    /// Authentication is handled upstream by <see cref="Middleware.TokenAuthMiddleware"/> (which returns a 401
    /// for unauthenticated requests), so a request that reaches this filter without the role is forbidden (403).
    /// </summary>
    public class AdminRoleAuthorizationFilter(SessionService sessionService) : IAuthorizationFilter
    {
        private readonly SessionService _sessionService = sessionService;

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            if (!_sessionService.IsInRole(nameof(ERole.Admin)))
            {
                context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
            }
        }
    }
}
