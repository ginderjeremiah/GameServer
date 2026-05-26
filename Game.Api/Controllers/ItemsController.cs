using Game.Api.Models.Common;
using Game.Api.Models.Items;
using Game.Abstractions.DataAccess;
using Microsoft.AspNetCore.Mvc;

namespace Game.Api.Controllers
{
    [Route("/api/[controller]/[action]")]
    [ApiController]
    public class ItemsController(IItems items) : ControllerBase
    {
        private readonly IItems _items = items;

        [HttpGet("/api/[controller]")]
        public ApiEnumerableResponse<Item> Items(bool refreshCache = false)
        {
            return ApiResponse.Success(_items.All(refreshCache).To().Model<Item>());
        }

        [HttpGet]
        public ApiEnumerableResponse<ItemModSlot> SlotsForItem(int itemId, bool refreshCache = false)
        {
            if (refreshCache)
                _items.All(refreshCache);

            var item = _items.LookupItem(itemId);
            return ApiResponse.Success((item?.ItemModSlots).To().Model<ItemModSlot>());
        }
    }
}
