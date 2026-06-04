using Game.Abstractions.DataAccess;
using Game.Api.Filters;
using Game.Api.Models.Common;
using Game.Api.Models.Tags;
using Microsoft.AspNetCore.Mvc;

namespace Game.Api.Controllers.Admin
{
    /// <summary>
    /// Admin Workbench endpoints for persisting tags. The route prefix is shared across every
    /// admin controller so the existing <c>/api/AdminTools/*</c> contract is preserved.
    /// </summary>
    [Route("/api/AdminTools/[action]")]
    [ApiController]
    [ServiceFilter(typeof(AdminRoleAuthorizationFilter))]
    [ServiceFilter(typeof(AdminCacheInvalidationFilter))]
    public class AdminTagsController(IEntityStore entityStore) : ControllerBase
    {
        private readonly IEntityStore _entityStore = entityStore;

        [HttpPost]
        public ApiResponse AddEditTags([FromBody] List<Change<Tag>> changes)
        {
            ChangeSetProcessor.Apply(changes,
                add: item => _entityStore.Insert(new Abstractions.Entities.Tag
                {
                    Name = item.Name,
                    TagCategoryId = item.TagCategoryId,
                }),
                edit: item => _entityStore.Update(new Abstractions.Entities.Tag
                {
                    Id = item.Id,
                    Name = item.Name,
                    TagCategoryId = item.TagCategoryId,
                }),
                delete: item => _entityStore.Delete(new Abstractions.Entities.Tag
                {
                    Id = item.Id,
                    Name = "",
                }));

            return ApiResponse.Success();
        }
    }
}
