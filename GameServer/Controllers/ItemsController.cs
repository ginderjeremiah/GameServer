using GameCore;
using GameCore.DataAccess;
using GameServer.Models.Common;
using GameServer.Models.Items;
using GameServer.Services;
using Microsoft.AspNetCore.Mvc;

namespace GameServer.Controllers
{
    [Route("/api/[controller]/[action]")]
    [ApiController]
    public class ItemsController : BaseController
    {
        public ItemsController(IRepositoryManager repositoryManager, IApiLogger logger, SessionService sessionService)
            : base(repositoryManager, logger, sessionService) { }

        [HttpGet("/api/[controller]")]
        public ApiListResponse<Item> Items(bool refreshCache = false)
        {
            var items = Repositories.Items.AllItems(refreshCache);
            return Success(items.Select(item => new Item(item)));
        }

        [HttpGet]
        public ApiListResponse<ItemSlot> SlotsForItem(int itemId, bool refreshCache = false)
        {
            var items = Repositories.Items.AllItems(refreshCache);

            return Success(items.Select(item => item.ItemSlots.Select(slot => new ItemSlot(slot))).FirstOrDefault() ?? []);
        }

        [HttpGet]
        public ApiListResponse<SlotType> SlotTypes()
        {
            return Success(Repositories.SlotTypes.AllSlotTypes().Select(type => new SlotType(type)));
        }
    }
}
