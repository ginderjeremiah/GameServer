using Game.Abstractions.Contracts;
using Game.Abstractions.DataAccess;
using Game.Api.Models.Common;
using Microsoft.AspNetCore.Mvc;

namespace Game.Api.Controllers
{
    [Route("/api/[controller]/[action]")]
    [ApiController]
    public class ItemsController(IItems items) : ControllerBase
    {
        private readonly IItems _items = items;

        [HttpGet]
        public ApiEnumerableResponse<ItemModSlot> SlotsForItem(int itemId, bool refreshCache = false)
        {
            var allItems = _items.All(refreshCache);
            IEnumerable<ItemModSlot> slots = itemId >= 0 && itemId < allItems.Count
                ? allItems[itemId].ModSlots
                : [];
            return ApiResponse.Success(slots);
        }
    }
}
