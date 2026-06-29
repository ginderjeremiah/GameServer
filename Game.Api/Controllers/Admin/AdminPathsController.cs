using Game.Abstractions.Contracts.Admin;
using Game.Abstractions.DataAccess.Admin;
using Game.Api.Filters;
using Game.Api.Models.Common;
using Microsoft.AspNetCore.Mvc;
// Disambiguate the reference-data Path from System.IO.Path (a global implicit using).
using Path = Game.Abstractions.Contracts.Path;

namespace Game.Api.Controllers.Admin
{
    /// <summary>
    /// Admin Workbench endpoints for persisting paths (the proficiency sequences, each declaring the activity
    /// key it trains on). A thin HTTP adapter over <see cref="IAdminPaths"/>. The route prefix is shared across
    /// every admin controller so the existing <c>/api/AdminTools/*</c> contract is preserved.
    /// </summary>
    [Route("/api/AdminTools/[action]")]
    [ApiController]
    [ServiceFilter(typeof(AdminRoleAuthorizationFilter))]
    [ReloadReferenceCaches]
    public class AdminPathsController(IAdminPaths adminPaths) : ControllerBase
    {
        private readonly IAdminPaths _adminPaths = adminPaths;

        [HttpPost]
        public ApiResponse AddEditPaths([FromBody] List<Change<Path>> changes)
        {
            return _adminPaths.SavePaths(changes);
        }
    }
}
