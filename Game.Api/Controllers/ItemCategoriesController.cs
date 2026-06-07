using Game.Abstractions.Contracts;
using Game.Abstractions.DataAccess;
using Game.Api.Models.Common;
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
            return ApiResponse.Success(_itemCategories.All());
        }
    }
}
