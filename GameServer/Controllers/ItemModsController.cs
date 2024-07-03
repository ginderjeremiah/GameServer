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
    public class ItemModsController : BaseController
    {
        public ItemModsController(IRepositoryManager repositoryManager, IApiLogger logger, SessionService sessionService)
            : base(repositoryManager, logger, sessionService) { }

        [HttpGet("/api/[controller]")]
        public ApiListResponse<ItemMod> ItemMods(bool refreshCache = false)
        {
            var itemMods = Repositories.ItemMods.AllItemMods(refreshCache);
            return Success(itemMods.Select(mod => new ItemMod(mod)));
        }

    }
}
