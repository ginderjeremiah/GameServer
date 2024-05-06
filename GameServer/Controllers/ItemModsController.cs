using GameCore;
using GameServer.Models.Common;
using GameServer.Models.Items;
using Microsoft.AspNetCore.Mvc;

namespace GameServer.Controllers
{
    [Route("/api/[controller]/[action]")]
    [ApiController]
    public class ItemModsController : BaseController
    {
        public ItemModsController(IRepositoryManager repositoryManager, IApiLogger logger)
            : base(repositoryManager, logger) { }

        [HttpGet("/api/[controller]")]
        public ApiListResponse<ItemMod> ItemMods(bool refreshCache = false)
        {
            return Success(Repositories.ItemMods.AllItemMods(refreshCache).Select(mod => new ItemMod(mod)));
        }

    }
}
