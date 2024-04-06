using DataAccess;
using GameLibrary;
using GameServer.Auth;
using GameServer.Models.Common;
using GameServer.Models.Tags;
using Microsoft.AspNetCore.Mvc;

namespace GameServer.Controllers
{
    [SessionAuthorize]
    [Route("/api/[controller]/[action]")]
    [ApiController]
    public class TagsController : BaseController
    {
        public TagsController(IRepositoryManager repositoryManager, IApiLogger logger)
            : base(repositoryManager, logger) { }

        [HttpGet("/api/[controller]")]
        public ApiListResponse<Tag> Tags()
        {
            return Success(Repositories.Tags.AllTags().Select(t => new Tag(t)));
        }

        [HttpGet]
        public ApiListResponse<Tag> TagsForItem(int itemId)
        {
            return Success(Repositories.Tags.TagsForItem(itemId).Select(t => new Tag(t)));
        }

        [HttpGet]
        public ApiListResponse<Tag> TagsForItemMod(int itemModId)
        {
            return Success(Repositories.Tags.TagsForItemMod(itemModId).Select(t => new Tag(t)));
        }
    }
}
