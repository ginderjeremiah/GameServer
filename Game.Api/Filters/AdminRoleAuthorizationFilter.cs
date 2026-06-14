using Game.Api.Models.Common;
using Game.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Game.Api.Filters
{
    /// <summary>
    /// Authorization filter that restricts an endpoint to users granted the <see cref="ERole.Admin"/> role.
    /// The role is read from the cryptographically validated <see cref="System.Security.Claims.ClaimsPrincipal"/>
    /// (the bearer handler projects the token's <c>role</c> claims onto it), so authorization derives from the
    /// token alone and never from the volatile gameplay session cache. Authentication is handled upstream by the
    /// JWT bearer authentication + authorization middleware (which returns a 401 for unauthenticated requests via
    /// the fallback policy), so a request that reaches this filter without the role is forbidden (403).
    /// </summary>
    public class AdminRoleAuthorizationFilter : IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            if (!context.HttpContext.User.IsInRole(nameof(ERole.Admin)))
            {
                context.Result = new ObjectResult(ApiResponse.Error("Forbidden: Admin role required."))
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
            }
        }
    }
}
