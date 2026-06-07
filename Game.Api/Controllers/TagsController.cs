using Game.Abstractions.Contracts;
using Game.Abstractions.DataAccess;
using Game.Api.Models.Common;
using Microsoft.AspNetCore.Mvc;

namespace Game.Api.Controllers
{
    [Route("/api/[controller]/[action]")]
    [ApiController]
    public class TagsController(ITags tags, ITagCategories tagCategories) : ControllerBase
    {
        private readonly ITags _tags = tags;
        private readonly ITagCategories _tagCategories = tagCategories;

        [HttpGet("/api/[controller]")]
        public ApiAsyncEnumerableResponse<Tag> Tags()
        {
            return ApiResponse.Success(_tags.All());
        }

        [HttpGet]
        public ApiAsyncEnumerableResponse<TagCategory> TagCategories()
        {
            return ApiResponse.Success(_tagCategories.All());
        }

        [HttpGet]
        public ApiAsyncEnumerableResponse<Tag> TagsForItem(int itemId)
        {
            return ApiResponse.Success(_tags.GetTagsForItem(itemId));
        }

        [HttpGet]
        public ApiAsyncEnumerableResponse<Tag> TagsForItemMod(int itemModId)
        {
            return ApiResponse.Success(_tags.GetTagsForItemMod(itemModId));
        }
    }
}
