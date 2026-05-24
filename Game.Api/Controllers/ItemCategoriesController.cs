using Game.Api.Models.Common;
using Game.Api.Models.Items;
using Game.Abstractions.DataAccess;
using Microsoft.AspNetCore.Mvc;

namespace Game.Api.Controllers
{
    [Route("/api/[controller]/[action]")]
    [ApiController]
    public class ItemCategoriesController(IItemCategories itemCategories) : ControllerBase
    {
        private readonly IItemCategories _itemCategories = itemCategories;

        [HttpGet("/api/[controller]")]
        public ApiAsyncEnumerableResponse<ItemCategory> ItemCategories()
        {
            return ApiResponse.Success(_itemCategories.All().To().Model<ItemCategory>());
        }
    }
}
