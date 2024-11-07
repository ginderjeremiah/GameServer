using Game.Api.Models.Common;
using Game.Api.Models.Items;
using Game.Core.DataAccess;
using Microsoft.AspNetCore.Mvc;

namespace Game.Api.Controllers
{
    [Route("/api/[controller]/[action]")]
    [ApiController]
    public class ItemModsController : ControllerBase
    {
        private readonly IRepositoryManager _repositoryManager;

        public ItemModsController(IRepositoryManager repositoryManager)
        {
            _repositoryManager = repositoryManager;
        }

        [HttpGet("/api/[controller]")]
        public ApiEnumerableResponse<ItemMod> ItemMods(bool refreshCache = false)
        {
            var itemMods = _repositoryManager.ItemMods.All(refreshCache).To().Model<ItemMod>();
            return ApiResponse.Success(itemMods);
        }
    }
}
