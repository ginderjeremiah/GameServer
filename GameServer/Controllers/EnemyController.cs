using DataAccess;
using DataAccess.Models.Enemies;
using GameLibrary;
using GameServer.Auth;
using GameServer.BattleSimulation;
using GameServer.Models.Common;
using GameServer.Models.Response;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace GameServer.Controllers
{
    [SessionAuthorize]
    [Route("/api/[controller]/[action]")]
    [ApiController]
    public class EnemyController : BaseController
    {
        public EnemyController(IRepositoryManager repositoryManager, ICacheManager cacheManager, IApiLogger logger)
            : base(repositoryManager, cacheManager, logger) { }

        [HttpGet]
        public ApiResponse<List<Enemy>> Enemies()
        {
            return Success(Caches.EnemyCache.AllEnemies());
        }

        [HttpGet]
        public ApiResponse<NewEnemyResponse> NewEnemy(int newZoneId = -1)
        {
            Log("Starting NewEnemy.");
            long startTime = Stopwatch.GetTimestamp();
            var now = DateTime.UtcNow;
            if (Session.EnemyCooldown > now)
            {
                return Success(new NewEnemyResponse
                {
                    Cooldown = (Session.EnemyCooldown - now).TotalMilliseconds
                });
            }

            if (newZoneId != -1 && newZoneId != Session.CurrentZone && Caches.ZoneCache.ValidateZoneId(newZoneId))
            {
                Session.CurrentZone = newZoneId;
            }

            var zone = Caches.ZoneCache.GetZone(Session.CurrentZone);
            var level = (int)new Random().NextInt64(zone.LevelMin, zone.LevelMax);
            var enemy = Caches.EnemyCache.GetRandomEnemy(zone.ZoneId);
            var seed = (uint)(now.Ticks % uint.MaxValue);
            var enemyInstance = new EnemyInstance()
            {
                EnemyId = enemy.EnemyId,
                EnemyLevel = level,
                Seed = seed
            };

            var simulator = new BattleSimulator(Session.PlayerData, enemy, enemyInstance, Caches.SkillCache);
            var victory = simulator.Simulate(out var totalMs);
            var earliestDefeat = now.AddMilliseconds(totalMs);

            Session.SetActiveEnemy(enemyInstance, earliestDefeat, victory);

            Log($"Finished NewEnemy: {Stopwatch.GetElapsedTime(startTime).TotalMilliseconds}");

            return Success(new NewEnemyResponse
            {
                EnemyInstance = enemyInstance
            });
        }

        [HttpPost]
        public ApiResponse<DefeatEnemyResponse> DefeatEnemy([FromBody] EnemyInstance enemyInstance)
        {
            lock (Session)
            {
                var now = DateTime.UtcNow;
                if (Session.ValidEnemyDefeat(enemyInstance))
                {
                    Session.ResetActiveEnemy();
                    Session.EnemyCooldown = now.AddSeconds(5);
                    var rewards = Session.GrantRewards(enemyInstance, Caches.LootCache);
                    return Success(new DefeatEnemyResponse
                    {
                        Cooldown = 5000,
                        Rewards = rewards
                    });
                }
                else
                {
                    HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                    return ErrorWithData("Enemy could not be defeated.", new DefeatEnemyResponse
                    {
                        Cooldown = (Session.EnemyCooldown - now).TotalMilliseconds
                    });
                }
            }
        }
    }
}
