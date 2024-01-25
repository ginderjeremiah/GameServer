using DataAccess;
using GameLibrary;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Diagnostics;

namespace GameServer.Auth
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    internal class SessionAuthorizeAttribute : Attribute, IAuthorizationFilter
    {
        public bool AllowAll { get; set; }

        public SessionAuthorizeAttribute(bool allowAll = false)
        {
            AllowAll = allowAll;
        }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            long startTime = Stopwatch.GetTimestamp();
            var logger = context.HttpContext.RequestServices.GetRequiredService<IApiLogger>();
            var config = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            var repos = context.HttpContext.RequestServices.GetRequiredService<IRepositoryManager>();
            logger.Log("Starting SessionAuthorize.");

            var token = context.HttpContext.Request.Cookies["sessionToken"];
            var tokenParts = token?.Split('.');
            var now = DateTime.UtcNow;

            if (tokenParts == null || tokenParts.Length != 3
                || !repos.SessionStore.TryGetSession(tokenParts[0].FromBase64(), out var sessionData)
                || !long.TryParse(tokenParts[1].FromBase64(), out var ticks)
                || ticks < now.Ticks
                || tokenParts[2].FromBase64() != $"{tokenParts[0]}.{tokenParts[1]}".Hash(sessionData.PlayerData.Salt.ToString(), 1))
            {
                if (!AllowAll)
                {
                    context.Result = new ForbidResult();
                }
                logger.LogError($"Failed SessionAuthorize: {Stopwatch.GetElapsedTime(startTime).TotalMilliseconds}");
                return;
            }

            var session = new Session(sessionData, repos);

            if (ticks < now.Add(Session.TokenLifetime / 2).Ticks)
            {
                context.HttpContext.Response.Cookies.Append("sessionToken", session.GetNewToken(), new CookieOptions()
                {
                    Secure = true,
                    HttpOnly = true,
                    Expires = now.Add(Session.TokenLifetime)
                });
            }

            context.HttpContext.Items["Session"] = session;
            logger.Log($"Succeeded SessionAuthorize: {Stopwatch.GetElapsedTime(startTime).TotalMilliseconds}");
        }
    }
}
