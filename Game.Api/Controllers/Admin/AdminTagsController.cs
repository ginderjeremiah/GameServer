using Game.Abstractions.DataAccess;
using Game.Api.Filters;
using Game.Api.Models.Common;
using Game.Api.Models.Tags;
using Microsoft.AspNetCore.Mvc;
using static Game.Api.EChangeType;

namespace Game.Api.Controllers.Admin
{
    /// <summary>
    /// Admin Workbench endpoints for persisting tags. The route prefix is shared across every
    /// admin controller so the existing <c>/api/AdminTools/*</c> contract is preserved.
    /// </summary>
    [Route("/api/AdminTools/[action]")]
    [ApiController]
    [ServiceFilter(typeof(AdminCacheInvalidationFilter))]
    public class AdminTagsController(IEntityStore entityStore) : ControllerBase
    {
        private readonly IEntityStore _entityStore = entityStore;

        [HttpPost]
        public ApiResponse AddEditTags([FromBody] List<Change<Tag>> changes)
        {
            foreach (var change in changes.OrderByDescending(c => c.ChangeType))
            {
                if (change.ChangeType == Add)
                {
                    _entityStore.Insert(new Abstractions.Entities.Tag
                    {
                        Name = change.Item.Name,
                        TagCategoryId = change.Item.TagCategoryId,
                    });
                }
                else if (change.ChangeType == Edit)
                {
                    _entityStore.Update(new Abstractions.Entities.Tag
                    {
                        Id = change.Item.Id,
                        Name = change.Item.Name,
                        TagCategoryId = change.Item.TagCategoryId,
                    });
                }
                else if (change.ChangeType == Delete)
                {
                    _entityStore.Delete(new Abstractions.Entities.Tag
                    {
                        Id = change.Item.Id,
                        Name = "",
                    });
                }
            }

            return ApiResponse.Success();
        }
    }
}
