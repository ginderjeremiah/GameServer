using Game.Abstractions.DataAccess;
using Game.Api.Models.Common;
using Game.Api.Models.Enemies;
using Microsoft.AspNetCore.Mvc;

namespace Game.Api.Controllers
{
    [Route("/api/[controller]/[action]")]
    [ApiController]
    public class EnemiesController(IEnemies enemies) : ControllerBase
    {
        private readonly IEnemies _enemies = enemies;

        [HttpGet("/api/[controller]")]
        public ApiEnumerableResponse<Enemy> Enemies(bool refreshCache = false)
        {
            return ApiResponse.Success(_enemies.All(refreshCache).To().Model<Enemy>());
        }
    }
}
