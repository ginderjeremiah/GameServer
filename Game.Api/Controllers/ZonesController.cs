using Game.Abstractions.DataAccess;
using Game.Api.Models.Common;
using Game.Api.Models.Zones;
using Microsoft.AspNetCore.Mvc;

namespace Game.Api.Controllers
{
    [Route("/api/[controller]/[action]")]
    [ApiController]
    public class ZonesController(IZones zones) : ControllerBase
    {
        private readonly IZones _zones = zones;

        [HttpGet]
        public ApiAsyncEnumerableResponse<ZoneEnemy> ZoneEnemies(int zoneId)
        {
            return ApiResponse.Success(_zones.ZoneEnemies(zoneId).To().Model<ZoneEnemy>());
        }
    }
}
