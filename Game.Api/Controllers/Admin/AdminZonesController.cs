using Game.Abstractions.DataAccess;
using Game.Api.Filters;
using Game.Api.Models.Common;
using Game.Api.Models.Zones;
using Microsoft.AspNetCore.Mvc;
using static Game.Api.EChangeType;

namespace Game.Api.Controllers.Admin
{
    /// <summary>
    /// Admin Workbench endpoints for persisting zones and their enemy spawn assignments. The
    /// route prefix is shared across every admin controller so the existing
    /// <c>/api/AdminTools/*</c> contract is preserved.
    /// </summary>
    [Route("/api/AdminTools/[action]")]
    [ApiController]
    [ServiceFilter(typeof(AdminRoleAuthorizationFilter))]
    [ServiceFilter(typeof(AdminCacheInvalidationFilter))]
    public class AdminZonesController(
        IZones zones,
        IEntityStore entityStore) : ControllerBase
    {
        private readonly IZones _zones = zones;
        private readonly IEntityStore _entityStore = entityStore;

        [HttpPost]
        public ApiResponse AddEditZones([FromBody] List<Change<Zone>> changes)
        {
            foreach (var change in changes.OrderByDescending(c => c.ChangeType))
            {
                if (change.ChangeType == Add)
                {
                    _entityStore.Insert(new Abstractions.Entities.Zone
                    {
                        Name = change.Item.Name,
                        Description = change.Item.Description,
                        LevelMin = change.Item.LevelMin,
                        LevelMax = change.Item.LevelMax,
                        Order = change.Item.Order,
                    });
                }
                else if (change.ChangeType == Edit)
                {
                    _entityStore.Update(new Abstractions.Entities.Zone
                    {
                        Id = change.Item.Id,
                        Name = change.Item.Name,
                        Description = change.Item.Description,
                        LevelMin = change.Item.LevelMin,
                        LevelMax = change.Item.LevelMax,
                        Order = change.Item.Order,
                    });
                }
                else if (change.ChangeType == Delete)
                {
                    _entityStore.Delete(new Abstractions.Entities.Zone
                    {
                        Id = change.Item.Id,
                        Name = "",
                        Description = "",
                    });
                }
            }

            return ApiResponse.Success();
        }

        [HttpPost]
        public ApiResponse SetZoneEnemies([FromBody] SetZoneEnemiesData zoneEnemiesData)
        {
            var zone = _zones.GetZone(zoneEnemiesData.ZoneId);
            if (zone is not null)
            {
                var newIds = zoneEnemiesData.ZoneEnemies.Select(ze => ze.EnemyId).ToList();
                foreach (var enemy in zone.ZoneEnemies.Where(e => !newIds.Contains(e.EnemyId)))
                {
                    _entityStore.Delete(enemy);
                }

                foreach (var enemy in zone.ZoneEnemies.Where(e => newIds.Contains(e.EnemyId)))
                {
                    var newData = zoneEnemiesData.ZoneEnemies.First(ze => ze.EnemyId == enemy.EnemyId);
                    _entityStore.Update(new Game.Abstractions.Entities.ZoneEnemy
                    {
                        ZoneId = enemy.ZoneId,
                        EnemyId = enemy.EnemyId,
                        Weight = newData.Weight,
                    });
                }

                var existingIds = zone.ZoneEnemies.Select(ze => ze.EnemyId).ToList();
                var zoneEnemies = zoneEnemiesData.ZoneEnemies
                    .Where(ze => !existingIds.Contains(ze.EnemyId))
                    .Select(ze => new Game.Abstractions.Entities.ZoneEnemy
                    {
                        ZoneId = zoneEnemiesData.ZoneId,
                        EnemyId = ze.EnemyId,
                        Weight = ze.Weight,
                    }).ToList();

                _entityStore.InsertAll(zoneEnemies);
                return ApiResponse.Success();
            }

            return ApiResponse.Error("Zone not found.");
        }
    }
}
