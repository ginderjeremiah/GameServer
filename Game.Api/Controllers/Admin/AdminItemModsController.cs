using Game.Abstractions.Contracts;
using Game.Abstractions.Contracts.Admin;
using Game.Abstractions.DataAccess.Admin;
using Game.Api.Filters;
using Game.Api.Models.Common;
using Microsoft.AspNetCore.Mvc;

namespace Game.Api.Controllers.Admin
{
    /// <summary>
    /// Admin Workbench endpoints for persisting item mods and their related collections (attributes
    /// and tags). A thin HTTP adapter over <see cref="IAdminItemMods"/>. The route prefix is shared
    /// across every admin controller so the existing <c>/api/AdminTools/*</c> contract is preserved.
    /// </summary>
    [Route("/api/AdminTools/[action]")]
    [ApiController]
    [ServiceFilter(typeof(AdminRoleAuthorizationFilter))]
    [ReloadReferenceCaches]
    public class AdminItemModsController(IAdminItemMods adminItemMods) : ControllerBase
    {
        private readonly IAdminItemMods _adminItemMods = adminItemMods;

        [HttpPost]
        public ApiResponse AddEditItemMods([FromBody] List<Change<ItemMod>> changes)
        {
            return _adminItemMods.SaveItemMods(changes)
                ? ApiResponse.Success()
                : ApiResponse.Error("Item mod not found.");
        }

        [HttpPost]
        public ApiResponse AddEditItemModAttributes([FromBody] AddEditAttributesData changeData)
        {
            return _adminItemMods.SetAttributes(changeData)
                ? ApiResponse.Success()
                : ApiResponse.Error("Item mod not found.");
        }

        [HttpPost]
        public async Task<ApiResponse> SetTagsForItemMod([FromBody] SetTagsData setTagsData)
        {
            return await _adminItemMods.SetTags(setTagsData)
                ? ApiResponse.Success()
                : ApiResponse.Error("Item mod not found.");
        }
    }
}
