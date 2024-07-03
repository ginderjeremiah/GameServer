using GameCore;
using GameCore.DataAccess;
using GameServer.Models.Common;
using GameServer.Models.Items;
using GameServer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GameServer.Controllers
{
    [Route("/api/[controller]/[action]")]
    [ApiController]
    public class ItemCategoriesController : BaseController
    {
        public ItemCategoriesController(IRepositoryManager repositoryManager, IApiLogger logger, SessionService sessionService)
            : base(repositoryManager, logger, sessionService) { }

        [HttpGet("/api/[controller]")]
        public async Task<ApiListResponse<ItemCategory>> ItemCategories()
        {
            return Success(await Repositories.ItemCategories.AllItemCategories().Select(cat => new ItemCategory(cat)).ToListAsync());
        }
    }
}
