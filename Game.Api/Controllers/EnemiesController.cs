using Game.Api.Models.Common;
using Game.Api.Models.Enemies;
using Game.Core.DataAccess;
using Microsoft.AspNetCore.Mvc;

namespace Game.Api.Controllers
{
    [Route("/api/[controller]/[action]")]
    [ApiController]
    public class EnemiesController : ControllerBase
    {
        private readonly IRepositoryManager _repositoryManager;

        public EnemiesController(IRepositoryManager repositoryManager)
        {
            _repositoryManager = repositoryManager;
        }

        [HttpGet("/api/[controller]")]
        public ApiEnumerableResponse<Enemy> Enemies(bool refreshCache = false)
        {
            var enemies = _repositoryManager.Enemies.All(refreshCache).To().Model<Enemy>();
            return ApiResponse.Success(enemies);
        }
    }
}
