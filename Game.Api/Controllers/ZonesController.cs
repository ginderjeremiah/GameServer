using Game.Api.Models.Common;
using Game.Api.Models.Zones;
using Game.Core.DataAccess;
using Microsoft.AspNetCore.Mvc;

namespace Game.Api.Controllers
{
    [Route("/api/[controller]/[action]")]
    [ApiController]
    public class ZonesController : ControllerBase
    {
        private readonly IRepositoryManager _repositoryManager;

        public ZonesController(IRepositoryManager repositoryManager)
        {
            _repositoryManager = repositoryManager;
        }

        [HttpGet("/api/[controller]")]
        public ApiEnumerableResponse<Zone> Zones()
        {
            var zones = _repositoryManager.Zones.All().To().Model<Zone>();
            return ApiResponse.Success(zones);
        }
    }
}
