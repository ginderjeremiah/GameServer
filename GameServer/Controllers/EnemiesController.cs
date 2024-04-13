using DataAccess;
using GameLibrary.Logging;
using GameServer.Auth;
using GameServer.BattleSimulation;
using GameServer.Models.Common;
using GameServer.Models.Enemies;
using Microsoft.AspNetCore.Mvc;

namespace GameServer.Controllers
{
    [SessionAuthorize]
    [Route("/api/[controller]/[action]")]
    [ApiController]
    public class EnemiesController : BaseController
    {
        public EnemiesController(IRepositoryManager repositoryManager, IApiLogger logger)
            : base(repositoryManager, logger) { }

        [HttpGet("/api/[controller]")]
        public ApiListResponse<Enemy> Enemies()
        {
            return Success(Repositories.Enemies.AllEnemies().Select(enemy => new Enemy(enemy)));
        }

        [HttpPost]
        public ApiResponse<DefeatEnemy> DefeatEnemy([FromBody] EnemyInstance enemyInstance)
        {
            var now = DateTime.UtcNow;
            if (Session.DefeatEnemy(enemyInstance))
            {
                Log($"DefeatEnemy: {{currentTime: {now:O}, earliestDefeat: {Session.EarliestDefeat:O}, difference: {(now - Session.EarliestDefeat).TotalMilliseconds} ms}}");
                Session.EnemyCooldown = now.AddSeconds(5);
                var rewards = Session.GrantRewards(enemyInstance);
                return Success(new DefeatEnemy
                {
                    Cooldown = 5000,
                    Rewards = rewards
                });
            }
            else
            {
                LogError($"DefeatEnemy: {{victory: {Session.Victory}, currentTime: {now:O}, earliestDefeat: {Session.EarliestDefeat:O}, difference: {(now - Session.EarliestDefeat).TotalMilliseconds} ms}}");
                return ErrorWithData("Enemy could not be defeated.", new DefeatEnemy
                {
                    Cooldown = (Session.EnemyCooldown - now).TotalMilliseconds
                });
            }
        }

        [HttpGet]
        public ApiResponse<NewEnemy> NewEnemy(int newZoneId = -1)
        {
            var now = DateTime.UtcNow;
            if (Session.EnemyCooldown > now)
            {
                return Success(new NewEnemy
                {
                    Cooldown = (Session.EnemyCooldown - now).TotalMilliseconds
                });
            }

            if (newZoneId != -1 && Repositories.Zones.ValidateZoneId(newZoneId))
            {
                Session.CurrentZone = newZoneId;
            }

            var zone = Repositories.Zones.GetZone(Session.CurrentZone);
            var level = (int)new Random().NextInt64(zone.LevelMin, zone.LevelMax);
            var enemy = Repositories.Enemies.GetRandomEnemy(zone.ZoneId);
            var seed = (uint)(now.Ticks % uint.MaxValue);
            var enemyInstance = new EnemyInstance()
            {
                EnemyId = enemy.EnemyId,
                Level = level,
                Seed = seed
            };

            var simulator = new BattleSimulator(Session.PlayerData, enemy, enemyInstance, Repositories);
            var victory = simulator.Simulate(out var totalMs);
            var earliestDefeat = now.AddMilliseconds(totalMs);

            Session.SetActiveEnemy(enemyInstance, earliestDefeat, victory);

            Log($"NewEnemy: {{victory: {victory}, battleTime: {totalMs} ms, now: {now:O}, earliestDefeat: {earliestDefeat:O}}}");

            return Success(new NewEnemy
            {
                EnemyInstance = enemyInstance
            });
        }
    }
}
