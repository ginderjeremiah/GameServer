using Game.Abstractions.Contracts;
using Game.Abstractions.Contracts.Admin;
using Game.Abstractions.DataAccess.Admin;
using Game.Api.Filters;
using Game.Api.Models.Common;
using Microsoft.AspNetCore.Mvc;

namespace Game.Api.Controllers.Admin
{
    /// <summary>
    /// Admin Workbench endpoint for persisting challenges. A challenge carries no child relationships,
    /// so it has a single whole-record Add/Edit/Delete endpoint. A thin HTTP adapter over
    /// <see cref="IAdminChallenges"/>. The route prefix is shared across every admin controller so the
    /// existing <c>/api/AdminTools/*</c> contract is preserved.
    /// </summary>
    [Route("/api/AdminTools/[action]")]
    [ApiController]
    [ServiceFilter(typeof(AdminRoleAuthorizationFilter))]
    [ReloadReferenceCaches]
    public class AdminChallengesController(IAdminChallenges adminChallenges) : ControllerBase
    {
        private readonly IAdminChallenges _adminChallenges = adminChallenges;

        [HttpPost]
        public ApiResponse AddEditChallenges([FromBody] List<Change<Challenge>> changes)
        {
            return _adminChallenges.SaveChallenges(changes);
        }
    }
}
