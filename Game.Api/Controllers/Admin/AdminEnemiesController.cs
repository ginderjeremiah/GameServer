using Game.Abstractions.DataAccess;
using Game.Api.Filters;
using Game.Api.Models.Common;
using Game.Api.Models.Enemies;
using Microsoft.AspNetCore.Mvc;
using static Game.Api.EChangeType;

namespace Game.Api.Controllers.Admin
{
    /// <summary>
    /// Admin Workbench endpoints for persisting enemies and their related collections
    /// (attribute distributions, skill pools, and zone spawns). The route prefix is shared
    /// across every admin controller so the existing <c>/api/AdminTools/*</c> contract is preserved.
    /// </summary>
    [Route("/api/AdminTools/[action]")]
    [ApiController]
    [ServiceFilter(typeof(AdminRoleAuthorizationFilter))]
    [ServiceFilter(typeof(AdminCacheInvalidationFilter))]
    public class AdminEnemiesController(
        IEnemies enemies,
        IEntityStore entityStore) : ControllerBase
    {
        private readonly IEnemies _enemies = enemies;
        private readonly IEntityStore _entityStore = entityStore;

        [HttpPost]
        public ApiResponse AddEditEnemies([FromBody] List<Change<Enemy>> changes)
        {
            foreach (var change in changes.OrderByDescending(c => c.ChangeType))
            {
                if (change.ChangeType == Add)
                {
                    _entityStore.Insert(new Abstractions.Entities.Enemy
                    {
                        Name = change.Item.Name,
                        IsBoss = change.Item.IsBoss,
                    });
                }
                else if (change.ChangeType == Edit)
                {
                    _entityStore.Update(new Abstractions.Entities.Enemy
                    {
                        Id = change.Item.Id,
                        Name = change.Item.Name,
                        IsBoss = change.Item.IsBoss,
                    });
                }
                else if (change.ChangeType == Delete)
                {
                    _entityStore.Delete(new Abstractions.Entities.Enemy
                    {
                        Id = change.Item.Id,
                        Name = "",
                    });
                }
            }

            return ApiResponse.Success();
        }

        [HttpPost]
        public ApiResponse SetEnemyAttributeDistributions([FromBody] SetEnemyAttributeDistributions distributionsData)
        {
            var enemy = _enemies.GetEnemy(distributionsData.EnemyId);
            if (enemy is not null)
            {
                var newIds = distributionsData.AttributeDistributions.Select(ad => (int)ad.AttributeId).ToList();
                foreach (var dist in enemy.AttributeDistributions.Where(ad => !newIds.Contains(ad.AttributeId)))
                {
                    _entityStore.Delete(dist);
                }

                foreach (var dist in enemy.AttributeDistributions.Where(ad => newIds.Contains(ad.AttributeId)))
                {
                    var newData = distributionsData.AttributeDistributions.First(ad => (int)ad.AttributeId == dist.AttributeId);
                    _entityStore.Update(new Abstractions.Entities.AttributeDistribution
                    {
                        EnemyId = enemy.Id,
                        AttributeId = dist.AttributeId,
                        BaseAmount = newData.BaseAmount,
                        AmountPerLevel = newData.AmountPerLevel
                    });
                }

                var existingIds = enemy.AttributeDistributions.Select(ad => ad.AttributeId).ToList();
                var newDistributions = distributionsData.AttributeDistributions
                    .Where(ad => !existingIds.Contains((int)ad.AttributeId))
                    .Select(ad => new Abstractions.Entities.AttributeDistribution
                    {
                        EnemyId = enemy.Id,
                        AttributeId = (int)ad.AttributeId,
                        BaseAmount = ad.BaseAmount,
                        AmountPerLevel = ad.AmountPerLevel
                    }).ToList();

                _entityStore.InsertAll(newDistributions);
                return ApiResponse.Success();
            }

            return ApiResponse.Error("Enemy not found.");
        }

        [HttpPost]
        public ApiResponse SetEnemySkills([FromBody] SetEnemySkillsData enemySkillsData)
        {
            var enemy = _enemies.GetEnemy(enemySkillsData.EnemyId);
            if (enemy is not null)
            {
                var newIds = enemySkillsData.SkillIds;
                foreach (var skill in enemy.EnemySkills.Where(e => !newIds.Contains(e.SkillId)))
                {
                    _entityStore.Delete(skill);
                }

                var existingIds = enemy.EnemySkills.Select(ze => ze.SkillId).ToList();
                var enemySkills = enemySkillsData.SkillIds
                    .Where(id => !existingIds.Contains(id))
                    .Select(id => new Abstractions.Entities.EnemySkill
                    {
                        EnemyId = enemy.Id,
                        SkillId = id,
                    }).ToList();

                _entityStore.InsertAll(enemySkills);
                return ApiResponse.Success();
            }

            return ApiResponse.Error("Enemy not found.");
        }

        [HttpPost]
        public ApiResponse SetEnemySpawns([FromBody] SetEnemySpawnsData spawnsData)
        {
            var enemy = _enemies.GetEnemy(spawnsData.EnemyId);
            if (enemy is not null)
            {
                var newZoneIds = spawnsData.Spawns.Select(s => s.ZoneId).ToList();
                foreach (var spawn in enemy.ZoneEnemies.Where(ze => !newZoneIds.Contains(ze.ZoneId)))
                {
                    _entityStore.Delete(spawn);
                }

                foreach (var spawn in enemy.ZoneEnemies.Where(ze => newZoneIds.Contains(ze.ZoneId)))
                {
                    var newData = spawnsData.Spawns.First(s => s.ZoneId == spawn.ZoneId);
                    _entityStore.Update(new Abstractions.Entities.ZoneEnemy
                    {
                        ZoneId = spawn.ZoneId,
                        EnemyId = enemy.Id,
                        Weight = newData.Weight,
                    });
                }

                var existingZoneIds = enemy.ZoneEnemies.Select(ze => ze.ZoneId).ToList();
                var newSpawns = spawnsData.Spawns
                    .Where(s => !existingZoneIds.Contains(s.ZoneId))
                    .Select(s => new Abstractions.Entities.ZoneEnemy
                    {
                        ZoneId = s.ZoneId,
                        EnemyId = enemy.Id,
                        Weight = s.Weight,
                    }).ToList();

                _entityStore.InsertAll(newSpawns);
                return ApiResponse.Success();
            }

            return ApiResponse.Error("Enemy not found.");
        }
    }
}
