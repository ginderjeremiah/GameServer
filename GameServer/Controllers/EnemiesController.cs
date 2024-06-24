﻿using GameCore;
using GameCore.BattleSimulation;
using GameServer.Auth;
using GameServer.Models.Common;
using GameServer.Models.Enemies;
using GameServer.Services;
using Microsoft.AspNetCore.Mvc;
using EnemyInstance = GameCore.BattleSimulation.EnemyInstance;
using EnemyInstanceModel = GameServer.Models.Enemies.EnemyInstance;

namespace GameServer.Controllers
{
    [Route("/api/[controller]/[action]")]
    [ApiController]
    public class EnemiesController : BaseController
    {
        public EnemiesController(IRepositoryManager repositoryManager, IApiLogger logger, SessionService sessionService)
            : base(repositoryManager, logger, sessionService) { }

        [HttpGet("/api/[controller]")]
        public async Task<ApiListResponse<Enemy>> Enemies()
        {
            var enemies = await Repositories.Enemies.AllEnemiesAsync();
            return Success(enemies.Select(enemy => new Enemy(enemy)));
        }

        [SessionAuthorize]
        [HttpPost]
        public async Task<ApiResponse<DefeatEnemy>> DefeatEnemy([FromBody] EnemyInstanceModel enemyInstance)
        {
            var now = DateTime.UtcNow;
            var instance = new EnemyInstance
            {
                Id = enemyInstance.Id,
                Level = enemyInstance.Level,
                Seed = enemyInstance.Seed,
                SelectedSkills = enemyInstance.SelectedSkills,
                Attributes = enemyInstance.Attributes.Select(att => new BattlerAttribute { AttributeId = att.AttributeId, Amount = att.Amount }).ToList()
            };
            if (Session.DefeatEnemy(instance))
            {
                Logger.LogDebug($"DefeatEnemy: {{currentTime: {now:O}, earliestDefeat: {Session.EarliestDefeat:O}, difference: {(now - Session.EarliestDefeat).TotalMilliseconds} ms}}");
                Session.EnemyCooldown = now.AddSeconds(5);
                var rewards = await Session.GrantRewards(instance);
                return Success(new DefeatEnemy
                {
                    Cooldown = 5000,
                    Rewards = new Models.Enemies.DefeatRewards(rewards)
                });
            }
            else
            {
                Logger.LogError($"DefeatEnemy: {{victory: {Session.Victory}, currentTime: {now:O}, earliestDefeat: {Session.EarliestDefeat:O}, difference: {(now - Session.EarliestDefeat).TotalMilliseconds} ms}}");
                return ErrorWithData("Enemy could not be defeated.", new DefeatEnemy
                {
                    Cooldown = (Session.EnemyCooldown - now).TotalMilliseconds
                });
            }
        }

        [SessionAuthorize]
        [HttpGet]
        public async Task<ApiResponse<NewEnemy>> NewEnemy(int newZoneId = -1)
        {
            var now = DateTime.UtcNow;
            if (Session.EnemyCooldown > now)
            {
                return Success(new NewEnemy
                {
                    Cooldown = (Session.EnemyCooldown - now).TotalMilliseconds
                });
            }

            if (newZoneId != -1 && await Repositories.Zones.ValidateZoneIdAsync(newZoneId))
            {
                Session.CurrentZone = newZoneId;
            }

            var zone = await Repositories.Zones.GetZoneAsync(Session.CurrentZone);
            var level = (int)new Random().NextInt64(zone.LevelMin, zone.LevelMax);
            var enemy = await Repositories.Enemies.GetRandomEnemyAsync(zone.Id);
            var seed = (uint)(now.Ticks % uint.MaxValue);
            var enemyInstance = new EnemyInstance()
            {
                Id = enemy.Id,
                Level = level,
                Seed = seed
            };

            var simulator = new BattleSimulator(Session, enemy, enemyInstance, Repositories);
            var victory = simulator.Simulate(out var totalMs);
            var earliestDefeat = now.AddMilliseconds(totalMs);

            Session.SetActiveEnemy(enemyInstance, earliestDefeat, victory);

            Logger.LogDebug($"NewEnemy: {{victory: {victory}, battleTime: {totalMs} ms, now: {now:O}, earliestDefeat: {earliestDefeat:O}}}");

            return Success(new NewEnemy
            {
                EnemyInstance = new EnemyInstanceModel(enemyInstance)
            });
        }
    }
}
