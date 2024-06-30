using GameCore;
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
        public async Task<ApiListResponse<ItemMod>> ItemMods(bool refreshCache = false)
        {
            var itemMods = await Repositories.ItemMods.AllItemModsAsync(refreshCache);
            return Success(itemMods.Select(mod => new ItemMod(mod)));
        }

    }
}
