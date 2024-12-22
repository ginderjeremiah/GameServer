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
        public ApiEnumerableResponse<Zone> Zones(bool refreshCache = false)
        {
            var zones = _repositoryManager.Zones.All(refreshCache).To().Model<Zone>();
            return ApiResponse.Success(zones);
        }

        [HttpGet]
        public ApiAsyncEnumerableResponse<ZoneEnemy> ZoneEnemies(int zoneId)
        {
            var zoneEnemies = _repositoryManager.Zones.ZoneEnemies(zoneId).To().Model<ZoneEnemy>();
            return ApiResponse.Success(zoneEnemies);
        }
    }
}
