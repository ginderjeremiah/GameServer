using DataAccess;
using DataAccess.Models.ItemCategories;
using GameLibrary;
using GameServer.Auth;
using GameServer.Models.Common;
using Microsoft.AspNetCore.Mvc;

namespace GameServer.Controllers
{
    [SessionAuthorize]
    [Route("/api/[controller]/[action]")]
    [ApiController]
    public class ItemCategoriesController : BaseController
    {
        public ItemCategoriesController(IRepositoryManager repositoryManager, IApiLogger logger)
            : base(repositoryManager, logger) { }

        [HttpGet("/api/[controller]")]
        public ApiResponse<List<ItemCategory>> ItemCategories()
        {
            return Success(Repositories.ItemCategories.GetItemCategories());
        }
    }
}
