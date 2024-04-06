﻿using DataAccess;
using DataAccess.Models.Enemies;
using GameLibrary;
using GameServer.Auth;
using GameServer.BattleSimulation;
using GameServer.Models.Common;
using GameServer.Models.Response;
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
        public ApiResponse<List<Enemy>> Enemies()
        {
            return Success(Repositories.Enemies.AllEnemies());
        }

        [HttpPost]
        public ApiResponse<DefeatEnemy> DefeatEnemy([FromBody] EnemyInstance enemyInstance)
        {
            lock (Session)
            {
                var now = DateTime.UtcNow;
                if (Session.ValidEnemyDefeat(enemyInstance))
                {
                    Session.ResetActiveEnemy();
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
                    return ErrorWithData("Enemy could not be defeated.", new DefeatEnemy
                    {
                        Cooldown = (Session.EnemyCooldown - now).TotalMilliseconds
                    });
                }
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
                EnemyLevel = level,
                Seed = seed
            };

            var simulator = new BattleSimulator(Session.PlayerData, enemy, enemyInstance, Repositories.Skills.AllSkills());
            var victory = simulator.Simulate(out var totalMs);
            var earliestDefeat = now.AddMilliseconds(totalMs);

            Session.SetActiveEnemy(enemyInstance, earliestDefeat, victory);

            return Success(new NewEnemy
            {
                EnemyInstance = enemyInstance
            });
        }
    }
}