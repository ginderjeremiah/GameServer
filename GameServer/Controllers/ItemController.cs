using DataAccess;
using DataAccess.Models.Items;
using DataAccess.Models.SlotTypes;
using GameLibrary;
using GameServer.Auth;
using GameServer.Models;
using GameServer.Models.Common;
using Microsoft.AspNetCore.Mvc;

namespace GameServer.Controllers
{
    [SessionAuthorize]
    [Route("/api/[controller]/[action]")]
    [ApiController]
    public class ItemController : BaseController
    {
        public ItemController(IRepositoryManager repositoryManager, IApiLogger logger)
            : base(repositoryManager, logger) { }

        [HttpGet]
        public ApiResponse<List<Item>> Items()
        {
            return Success(Repositories.Items.AllItems());
        }

        [HttpGet]
        public ApiResponse<List<ItemSlot>> SlotsForItem(int itemId, bool refreshCache = false)
        {
            return Success(Repositories.ItemSlots.SlotsForItem(itemId, refreshCache));
        }

        [HttpGet]
        public ApiResponse<List<SlotType>> SlotTypes()
        {
            return Success(Repositories.SlotTypes.AllSlotTypes());
        }
    }
}
