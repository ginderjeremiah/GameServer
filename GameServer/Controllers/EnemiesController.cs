using GameCore;
using GameCore.DataAccess;
using GameServer.Models.Common;
using GameServer.Models.Enemies;
using GameServer.Services;
using Microsoft.AspNetCore.Mvc;

namespace GameServer.Controllers
{
    [Route("/api/[controller]/[action]")]
    [ApiController]
    public class EnemiesController : BaseController
    {
        public EnemiesController(IRepositoryManager repositoryManager, IApiLogger logger, SessionService sessionService)
            : base(repositoryManager, logger, sessionService) { }

        [HttpGet("/api/[controller]")]
        public ApiListResponse<Enemy> Enemies()
        {
            var enemies = Repositories.Enemies.AllEnemies();
            return Success(enemies.Select(enemy => new Enemy(enemy)));
        }
    }
}
