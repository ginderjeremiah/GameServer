using Game.Abstractions.Contracts.Admin;
using Game.Api.Filters;
using Game.Api.Models.Common;
using Game.Application.Content;
using Microsoft.AspNetCore.Mvc;

namespace Game.Api.Controllers.Admin
{
    /// <summary>
    /// Admin Ops endpoint for the content-graph lint: a read-only "Content Health" surface that runs the
    /// whole-graph reachability checks over the server's live reference caches and returns the findings with
    /// their error/warning counts (spike #1390, decision 5 — the live counterpart to the CI drift lint). A thin
    /// HTTP adapter over <see cref="IContentHealthService"/>. Shares the <c>/api/AdminTools/*</c> route prefix
    /// and requires the Admin role. Like the other Ops controllers it carries no
    /// <see cref="ReloadReferenceCachesAttribute"/>: it only reads the caches and mutates nothing.
    /// </summary>
    [Route("/api/AdminTools/[action]")]
    [ApiController]
    [ServiceFilter(typeof(AdminRoleAuthorizationFilter))]
    public class AdminContentHealthController(IContentHealthService contentHealth) : ControllerBase
    {
        private readonly IContentHealthService _contentHealth = contentHealth;

        [HttpGet]
        public ApiResponse<ContentHealthReport> GetContentHealth()
        {
            return ApiResponse.Success(_contentHealth.GetReport());
        }
    }
}
