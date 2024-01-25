using DataAccess;
using DataAccess.Models.Tags;
using GameLibrary;
using GameServer.Auth;
using GameServer.Models.Common;
using Microsoft.AspNetCore.Mvc;

namespace GameServer.Controllers
{
    [SessionAuthorize]
    [Route("/api/[controller]/[action]")]
    [ApiController]
    public class TagsController : BaseController
    {
        public TagsController(IRepositoryManager repositoryManager, ICacheManager cacheManager, IApiLogger logger)
            : base(repositoryManager, cacheManager, logger) { }

        [HttpGet]
        public ApiResponse<List<Tag>> Tags()
        {
            return Success(Repositories.Tags.AllTags());
        }

        [HttpGet]
        public ApiResponse<List<Tag>> TagsForItem(int itemId)
        {
            return Success(Repositories.Tags.TagsForItem(itemId));
        }

        [HttpGet]
        public ApiResponse<List<Tag>> TagsForItemMod(int itemModId)
        {
            return Success(Repositories.Tags.TagsForItemMod(itemModId));
        }
    }
}
