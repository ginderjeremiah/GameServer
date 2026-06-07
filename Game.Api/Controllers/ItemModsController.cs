using Game.Abstractions.Contracts;
using Game.Abstractions.DataAccess;
using Game.Api.Models.Common;
using Microsoft.AspNetCore.Mvc;

namespace Game.Api.Controllers
{
    [Route("/api/[controller]/[action]")]
    [ApiController]
    public class ItemModsController(IItemModTypes itemModTypes) : ControllerBase
    {
        private readonly IItemModTypes _itemModTypes = itemModTypes;

        [HttpGet]
        public ApiAsyncEnumerableResponse<ItemModType> ItemModTypes()
        {
            return ApiResponse.Success(_itemModTypes.All());
        }
    }
}
