using DataAccess;
using GameCore.Logging.Interfaces;
using GameServer.Models.Common;
using GameServer.Models.Items;
using Microsoft.AspNetCore.Mvc;

namespace GameServer.Controllers
{
    [Route("/api/[controller]/[action]")]
    [ApiController]
    public class ItemsController : BaseController
    {
        public ItemsController(IRepositoryManager repositoryManager, IApiLogger logger)
            : base(repositoryManager, logger) { }

        [HttpGet("/api/[controller]")]
        public ApiListResponse<Item> Items()
        {
            return Success(Repositories.Items.AllItems().Select(item => new Item(item)));
        }

        [HttpGet]
        public ApiListResponse<ItemSlot> SlotsForItem(int itemId, bool refreshCache = false)
        {
            return Success(Repositories.ItemSlots.SlotsForItem(itemId, refreshCache).Select(slot => new ItemSlot(slot)));
        }

        [HttpGet]
        public ApiListResponse<SlotType> SlotTypes()
        {
            return Success(Repositories.SlotTypes.AllSlotTypes().Select(type => new SlotType(type)));
        }

        //[HttpGet]
        //public ApiListResponse<ItemSlot> SlotsForItem(int itemId, bool refreshCache = false)
        //{
        //    return Success(Repositories.ItemSlots.SlotsForItem(itemId, refreshCache).Select(slot => new ItemSlot(slot)));
        //}
    }
}
