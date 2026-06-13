using Game.Abstractions;
using Game.Abstractions.Contracts.Admin;
using Game.Abstractions.DataAccess;
using Game.Abstractions.DataAccess.Admin;
using Contracts = Game.Abstractions.Contracts;
using Entities = Game.Infrastructure.Entities;

namespace Game.DataAccess.Repositories.Admin
{
    /// <summary>
    /// Content Authoring persistence for zones and their enemy spawn assignments. Reuses the cached
    /// entity lookups (<see cref="IZoneEntityCache.LookupZone"/>, <see cref="IEnemyEntityCache.GetEnemy"/>)
    /// for existence/diff and builds fresh, navigation-free entities for every write.
    /// </summary>
    internal class AdminZones(
        IZoneEntityCache zones,
        IEnemyEntityCache enemies,
        IChallenges challenges,
        IEntityStore entityStore) : IAdminZones
    {
        private readonly IZoneEntityCache _zones = zones;
        private readonly IEnemyEntityCache _enemies = enemies;
        private readonly IChallenges _challenges = challenges;
        private readonly IEntityStore _entityStore = entityStore;

        public string? SaveZones(IReadOnlyList<Change<Contracts.Zone>> changes)
        {
            // A zone's dedicated boss must reference an existing enemy flagged IsBoss, and its unlock gate
            // must reference an existing challenge. Validate the whole change set up front so an invalid
            // reference rejects the batch rather than partially applying.
            var challengeCount = _challenges.All().Count;
            foreach (var change in changes)
            {
                if (change.ChangeType == EChangeType.Delete)
                {
                    continue;
                }

                if (change.Item.BossEnemyId is int bossEnemyId
                    && _enemies.GetEnemy(bossEnemyId) is not { IsBoss: true })
                {
                    return "Boss enemy is invalid. A zone's boss must be an existing enemy marked as a boss.";
                }

                // Challenges are zero-based-id reference data, so a valid id is an in-range index.
                if (change.Item.UnlockChallengeId is int unlockChallengeId
                    && (unlockChallengeId < 0 || unlockChallengeId >= challengeCount))
                {
                    return "Unlock challenge is invalid. A zone's unlock challenge must reference an existing challenge.";
                }
            }

            ChangeSetProcessor.Apply(changes,
                add: item => _entityStore.Insert(new Entities.Zone
                {
                    Name = item.Name,
                    Description = item.Description,
                    LevelMin = item.LevelMin,
                    LevelMax = item.LevelMax,
                    Order = item.Order,
                    BossEnemyId = item.BossEnemyId,
                    BossLevel = item.BossLevel,
                    UnlockChallengeId = item.UnlockChallengeId,
                }),
                edit: item => _entityStore.Update(new Entities.Zone
                {
                    Id = item.Id,
                    Name = item.Name,
                    Description = item.Description,
                    LevelMin = item.LevelMin,
                    LevelMax = item.LevelMax,
                    Order = item.Order,
                    BossEnemyId = item.BossEnemyId,
                    BossLevel = item.BossLevel,
                    UnlockChallengeId = item.UnlockChallengeId,
                    RetiredAt = item.RetiredAt,
                }));

            return null;
        }

        public bool SetEnemies(SetZoneEnemiesData data)
        {
            var zone = _zones.LookupZone(data.ZoneId);
            if (zone is null)
            {
                return false;
            }

            var newIds = data.ZoneEnemies.Select(ze => ze.EnemyId).ToList();
            foreach (var enemy in zone.ZoneEnemies.Where(e => !newIds.Contains(e.EnemyId)))
            {
                _entityStore.Delete(new Entities.ZoneEnemy
                {
                    ZoneId = enemy.ZoneId,
                    EnemyId = enemy.EnemyId,
                });
            }

            foreach (var enemy in zone.ZoneEnemies.Where(e => newIds.Contains(e.EnemyId)))
            {
                var newData = data.ZoneEnemies.First(ze => ze.EnemyId == enemy.EnemyId);
                _entityStore.Update(new Entities.ZoneEnemy
                {
                    ZoneId = enemy.ZoneId,
                    EnemyId = enemy.EnemyId,
                    Weight = newData.Weight,
                });
            }

            var existingIds = zone.ZoneEnemies.Select(ze => ze.EnemyId).ToList();
            var zoneEnemies = data.ZoneEnemies
                .Where(ze => !existingIds.Contains(ze.EnemyId))
                .Select(ze => new Entities.ZoneEnemy
                {
                    ZoneId = data.ZoneId,
                    EnemyId = ze.EnemyId,
                    Weight = ze.Weight,
                }).ToList();

            _entityStore.InsertAll(zoneEnemies);
            return true;
        }
    }
}
