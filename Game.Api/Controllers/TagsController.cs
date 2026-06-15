using Game.Abstractions.Contracts;
using Game.Abstractions.DataAccess;
using Game.Api.Filters;
using Game.Api.Models.Common;
using Microsoft.AspNetCore.Mvc;

namespace Game.Api.Controllers
{
    [Route("/api/[controller]/[action]")]
    [ApiController]
    [ServiceFilter(typeof(AdminRoleAuthorizationFilter))]
    public class TagsController(ITags tags, ITagCategories tagCategories) : ControllerBase
    {
        private readonly ITags _tags = tags;
        private readonly ITagCategories _tagCategories = tagCategories;

        [HttpGet("/api/[controller]")]
        public ApiAsyncEnumerableResponse<Tag> Tags()
        {
            return ApiResponse.Success(_tags.All());
        }

        [HttpGet]
        public ApiAsyncEnumerableResponse<TagCategory> TagCategories()
        {
            return ApiResponse.Success(_tagCategories.All());
        }
    }
}
