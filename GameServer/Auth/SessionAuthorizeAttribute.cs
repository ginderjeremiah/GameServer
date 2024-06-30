using Microsoft.AspNetCore.Mvc.Filters;

namespace GameServer.Auth
{
    [AttributeUsage(AttributeTargets.Method)]
    internal class SessionAuthorizeAttribute : Attribute, IAuthorizationFilter
    {
        public bool AllowAll { get; set; }

        public SessionAuthorizeAttribute(bool allowAll = false)
        {
            AllowAll = allowAll;
        }

        public void OnAuthorization(AuthorizationFilterContext context) { }
    }
}
