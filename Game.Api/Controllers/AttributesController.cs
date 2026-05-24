using Game.Abstractions.DataAccess;
using Game.Api.Models.Common;
using Microsoft.AspNetCore.Mvc;
using Attribute = Game.Api.Models.Attributes.Attribute;

namespace Game.Api.Controllers
{
    [Route("/api/[controller]/[action]")]
    [ApiController]
    public class AttributesController(IAttributes attributes) : ControllerBase
    {
        private readonly IAttributes _attributes = attributes;

        [HttpGet("/api/[controller]")]
        public ApiAsyncEnumerableResponse<Attribute> Attributes()
        {
            return ApiResponse.Success(_attributes.All().To().Model<Attribute>());
        }
    }
}
