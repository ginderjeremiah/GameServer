using Game.Abstractions.Contracts;
using Game.Abstractions.Contracts.Admin;
using Game.Abstractions.DataAccess.Admin;
using Game.Api.Filters;
using Game.Api.Models.Common;
using Microsoft.AspNetCore.Mvc;

namespace Game.Api.Controllers.Admin
{
    /// <summary>
    /// Admin Workbench endpoint for persisting tags. A thin HTTP adapter over <see cref="IAdminTags"/>.
    /// The route prefix is shared across every admin controller so the existing <c>/api/AdminTools/*</c>
    /// contract is preserved.
    /// </summary>
    [Route("/api/AdminTools/[action]")]
    [ApiController]
    [ServiceFilter(typeof(AdminRoleAuthorizationFilter))]
    [ReloadReferenceCaches]
    public class AdminTagsController(IAdminTags adminTags) : ControllerBase
    {
        private readonly IAdminTags _adminTags = adminTags;

        [HttpPost]
        public ApiResponse AddEditTags([FromBody] List<Change<Tag>> changes)
        {
            _adminTags.SaveTags(changes);
            return ApiResponse.Success();
        }
    }
}
