using Game.Api.Models.Common;
using Game.Api.Models.Items;
using Game.Core.DataAccess;
using Microsoft.AspNetCore.Mvc;

namespace Game.Api.Controllers
{
    [Route("/api/[controller]/[action]")]
    [ApiController]
    public class ItemCategoriesController : ControllerBase
    {
        private readonly IRepositoryManager _repositoryManager;

        public ItemCategoriesController(IRepositoryManager repositoryManager)
        {
            _repositoryManager = repositoryManager;
        }

        [HttpGet("/api/[controller]")]
        public ApiAsyncEnumerableResponse<ItemCategory> ItemCategories()
        {
            var itemCategories = _repositoryManager.ItemCategories.All().To().Model<ItemCategory>();
            return ApiResponse.Success(itemCategories);
        }
    }
}
