using GameCore;
using GameCore.Sessions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Diagnostics;

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

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            long startTime = Stopwatch.GetTimestamp();
            var logger = context.HttpContext.RequestServices.GetRequiredService<IApiLogger>();
            var repos = context.HttpContext.RequestServices.GetRequiredService<IRepositoryManager>();
            logger.LogDebug("Starting SessionAuthorize.");

            var token = context.HttpContext.Request.Cookies[Constants.TOKEN_NAME];
            var tokenParts = token?.Split('.');
            var now = DateTime.UtcNow;

            if (tokenParts == null || tokenParts.Length != 3
                || !long.TryParse(tokenParts[1].FromBase64(), out var ticks)
                || ticks < now.Ticks
                || !repos.SessionStore.TryGetSession(tokenParts[0].FromBase64(), out var sessionData)
                || tokenParts[2].FromBase64() != $"{tokenParts[0]}.{tokenParts[1]}".Hash(sessionData.PlayerData.Salt.ToString(), 1))
            {
                if (!AllowAll)
                {
                    context.Result = new ForbidResult();
                }
                logger.LogError($"Failed SessionAuthorize: {Stopwatch.GetElapsedTime(startTime).TotalMilliseconds} ms");
                return;
            }

            var session = new Session(sessionData, repos);

            if (ticks < now.Add(Constants.TOKEN_LIFETIME / 2).Ticks)
            {
                context.HttpContext.Response.Cookies.Append(Constants.TOKEN_NAME, session.GetNewToken(), new CookieOptions()
                {
                    Secure = true,
                    HttpOnly = true,
                    Expires = now.Add(Constants.TOKEN_LIFETIME)
                });
            }

            context.HttpContext.Items["Session"] = session;
            logger.LogDebug($"Succeeded SessionAuthorize: {Stopwatch.GetElapsedTime(startTime).TotalMilliseconds} ms");
        }
    }
}
