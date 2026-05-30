using Game.Api.Models.Common;
using Microsoft.AspNetCore.Mvc;
using Attribute = Game.Api.Models.Attributes.Attribute;

namespace Game.Api.Controllers
{
    [Route("/api/[controller]/[action]")]
    [ApiController]
    public class AttributesController() : ControllerBase
    {
        [HttpGet("/api/[controller]")]
        public ApiEnumerableResponse<Attribute> Attributes()
        {
            return ApiResponse.Success(Core.Attributes.Attribute.GetAllAttributes().To().Model<Attribute>());
        }
    }
}
