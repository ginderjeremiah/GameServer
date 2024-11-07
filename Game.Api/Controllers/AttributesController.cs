using Game.Api.Models.Common;
using Game.Core.DataAccess;
using Microsoft.AspNetCore.Mvc;
using Attribute = Game.Api.Models.Attributes.Attribute;

namespace Game.Api.Controllers
{
    [Route("/api/[controller]/[action]")]
    [ApiController]
    public class AttributesController : ControllerBase
    {
        private readonly IRepositoryManager _repositoryManager;

        public AttributesController(IRepositoryManager repositoryManager)
        {
            _repositoryManager = repositoryManager;
        }

        [HttpGet("/api/[controller]")]
        public ApiAsyncEnumerableResponse<Attribute> Attributes()
        {
            var attributes = _repositoryManager.Attributes.All().To().Model<Attribute>();
            return ApiResponse.Success(attributes);
        }
    }
}
