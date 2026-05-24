using Game.Api.Models.Common;
using Game.Api.Models.Items;
using Game.Abstractions.DataAccess;
using Microsoft.AspNetCore.Mvc;

namespace Game.Api.Controllers
{
    [Route("/api/[controller]/[action]")]
    [ApiController]
    public class ItemModsController(IItemMods itemMods, IItemModTypes itemModTypes) : ControllerBase
    {
        private readonly IItemMods _itemMods = itemMods;
        private readonly IItemModTypes _itemModTypes = itemModTypes;

        [HttpGet("/api/[controller]")]
        public ApiEnumerableResponse<ItemMod> ItemMods(bool refreshCache = false)
        {
            return ApiResponse.Success(_itemMods.All(refreshCache).To().Model<ItemMod>());
        }

        [HttpGet]
        public ApiAsyncEnumerableResponse<ItemModType> ItemModTypes()
        {
            return ApiResponse.Success(_itemModTypes.All().To().Model<ItemModType>());
        }
    }
}
