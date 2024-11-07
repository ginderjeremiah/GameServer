using Game.Api.Models.Common;
using Game.Api.Models.Tags;
using Game.Core.DataAccess;
using Microsoft.AspNetCore.Mvc;

namespace Game.Api.Controllers
{
    [Route("/api/[controller]/[action]")]
    [ApiController]
    public class TagsController : ControllerBase
    {
        private readonly IRepositoryManager _repositoryManager;

        public TagsController(IRepositoryManager repositoryManager)
        {
            _repositoryManager = repositoryManager;
        }

        [HttpGet("/api/[controller]")]
        public ApiAsyncEnumerableResponse<Tag> Tags()
        {
            return ApiResponse.Success(_repositoryManager.Tags.All().To().Model<Tag>());
        }

        [HttpGet]
        public ApiAsyncEnumerableResponse<TagCategory> TagCategories()
        {
            return ApiResponse.Success(_repositoryManager.TagCategories.All().To().Model<TagCategory>());
        }

        [HttpGet]
        public ApiAsyncEnumerableResponse<Tag> TagsForItem(int itemId)
        {
            return ApiResponse.Success(_repositoryManager.Tags.GetTagsForItem(itemId).To().Model<Tag>());
        }

        [HttpGet]
        public ApiAsyncEnumerableResponse<Tag> TagsForItemMod(int itemModId)
        {
            return ApiResponse.Success(_repositoryManager.Tags.GetTagsForItemMod(itemModId).To().Model<Tag>());
        }
    }
}
