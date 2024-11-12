using Game.Api.Models.Common;
using Game.Api.Models.Items;
using Game.Core.DataAccess;
using Microsoft.AspNetCore.Mvc;

namespace Game.Api.Controllers
{
    [Route("/api/[controller]/[action]")]
    [ApiController]
    public class ItemsController : ControllerBase
    {
        private readonly IRepositoryManager _repositoryManager;

        public ItemsController(IRepositoryManager repositoryManager)
        {
            _repositoryManager = repositoryManager;
        }

        [HttpGet("/api/[controller]")]
        public ApiEnumerableResponse<Item> Items(bool refreshCache = false)
        {
            var items = _repositoryManager.Items.All(refreshCache).To().Model<Item>();
            return ApiResponse.Success(items);
        }

        [HttpGet]
        public ApiEnumerableResponse<ItemModSlot> SlotsForItem(int itemId, bool refreshCache = false)
        {
            if (refreshCache)
            {
                _repositoryManager.Items.All(refreshCache);
            }

            var item = _repositoryManager.Items.GetItem(itemId);

            return ApiResponse.Success((item?.ItemModSlots).To().Model<ItemModSlot>());
        }
    }
}
