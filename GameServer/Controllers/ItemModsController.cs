using DataAccess;
using DataAccess.Models.ItemMods;
using GameLibrary;
using GameServer.Auth;
using GameServer.Models.Common;
using Microsoft.AspNetCore.Mvc;

namespace GameServer.Controllers
{
    [SessionAuthorize]
    [Route("/api/[controller]/[action]")]
    [ApiController]
    public class ItemModsController : BaseController
    {
        public ItemModsController(IRepositoryManager repositoryManager, IApiLogger logger)
            : base(repositoryManager, logger) { }

        [HttpGet("/api/[controller]")]
        public ApiResponse<List<ItemMod>> ItemMods(bool refreshCache = false)
        {
            return Success(Repositories.ItemMods.AllItemMods(refreshCache));
        }

    }
}
