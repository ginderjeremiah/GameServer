using Game.Abstractions.Contracts;
using Game.Abstractions.Contracts.Admin;
using Game.Abstractions.DataAccess.Admin;
using Game.Api.Filters;
using Game.Api.Models.Common;
using Microsoft.AspNetCore.Mvc;

namespace Game.Api.Controllers.Admin
{
    /// <summary>
    /// Admin Workbench endpoints for persisting items and their related collections (attributes,
    /// item-mod slots, and tags). A thin HTTP adapter over <see cref="IAdminItems"/>. The route prefix
    /// is shared across every admin controller so the existing <c>/api/AdminTools/*</c> contract is preserved.
    /// </summary>
    [Route("/api/AdminTools/[action]")]
    [ApiController]
    [ServiceFilter(typeof(AdminRoleAuthorizationFilter))]
    [ServiceFilter(typeof(AdminCacheInvalidationFilter), Order = AdminCacheInvalidationFilter.FilterOrder)]
    public class AdminItemsController(IAdminItems adminItems) : ControllerBase
    {
        private readonly IAdminItems _adminItems = adminItems;

        [HttpPost]
        public ApiResponse AddEditItems([FromBody] List<Change<Item>> changes)
        {
            _adminItems.SaveItems(changes);
            return ApiResponse.Success();
        }

        [HttpPost]
        public ApiResponse AddEditItemAttributes([FromBody] AddEditAttributesData changeData)
        {
            return _adminItems.SetAttributes(changeData)
                ? ApiResponse.Success()
                : ApiResponse.Error("Item does not exist.");
        }

        [HttpPost]
        public ApiResponse AddEditItemModSlots([FromBody] List<Change<ItemModSlot>> changes)
        {
            _adminItems.SaveModSlots(changes);
            return ApiResponse.Success();
        }

        [HttpPost]
        public async Task<ApiResponse> SetTagsForItem([FromBody] SetTagsData setTagsData)
        {
            return await _adminItems.SetTags(setTagsData)
                ? ApiResponse.Success()
                : ApiResponse.Error("Item not found.");
        }
    }
}
