using Game.Api.Models.Common;
using Game.Api.Models.Tags;
using Game.Abstractions.DataAccess;
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
            return ApiResponse.Success(_tags.All().To().Model<Tag>());
        }

        [HttpGet]
        public ApiAsyncEnumerableResponse<TagCategory> TagCategories()
        {
            return ApiResponse.Success(_tagCategories.All().To().Model<TagCategory>());
        }

        [HttpGet]
        public ApiAsyncEnumerableResponse<Tag> TagsForItem(int itemId)
        {
            return ApiResponse.Success(_tags.GetTagsForItem(itemId).To().Model<Tag>());
        }

        [HttpGet]
        public ApiAsyncEnumerableResponse<Tag> TagsForItemMod(int itemModId)
        {
            return ApiResponse.Success(_tags.GetTagsForItemMod(itemModId).To().Model<Tag>());
        }
    }
}
