using GameCore;
using GameServer.Models.Common;
using GameServer.Models.Tags;
using GameServer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GameServer.Controllers
{
    [Route("/api/[controller]/[action]")]
    [ApiController]
    public class TagsController : BaseController
    {
        public TagsController(IRepositoryManager repositoryManager, IApiLogger logger, SessionService sessionService)
            : base(repositoryManager, logger, sessionService) { }

        [HttpGet("/api/[controller]")]
        public async Task<ApiListResponse<Tag>> Tags()
        {
            return Success(await Repositories.Tags.AllTags().Select(t => new Tag(t)).ToListAsync());
        }

        [HttpGet]
        public async Task<ApiListResponse<TagCategory>> TagCategories()
        {
            return Success(await Repositories.TagCategories.AllTagCategories().Select(tc => new TagCategory(tc)).ToListAsync());
        }

        [HttpGet]
        public async Task<ApiListResponse<Tag>> TagsForItem(int itemId)
        {
            return Success(await Repositories.Tags.TagsForItem(itemId).Select(t => new Tag(t)).ToListAsync());
        }

        [HttpGet]
        public async Task<ApiListResponse<Tag>> TagsForItemMod(int itemModId)
        {
            return Success(await Repositories.Tags.TagsForItemMod(itemModId).Select(t => new Tag(t)).ToListAsync());
        }
    }
}
