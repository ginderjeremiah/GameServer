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

        public AdminSaveResult SaveZones(IReadOnlyList<Change<Contracts.Zone>> changes)
        {
            // A zone's dedicated boss must reference an existing enemy flagged IsBoss, and its unlock gate
            // must reference an existing challenge. An edit must also target an existing zone — a missing id
            // is a not-found rejection, not an EF 0-row update that throws. Validate the whole change set up
            // front so an invalid reference rejects the batch rather than partially applying.
            foreach (var change in changes)
            {
                if (change.ChangeType == EChangeType.Delete)
                {
                    continue;
                }

                if (change.ChangeType == EChangeType.Edit && _zones.LookupZone(change.Item.Id) is null)
                {
                    return AdminSaveResult.NotFound("Zone");
                }

                if (change.Item.BossEnemyId is int bossEnemyId
                    && _enemies.GetEnemy(bossEnemyId) is not { IsBoss: true })
                {
                    return AdminSaveResult.Failure("Boss enemy is invalid. A zone's boss must be an existing enemy marked as a boss.");
                }

                // Challenges are zero-based-id reference data, so a valid id is an in-range index (an O(1)
                // check, like the enemy/zone validators above).
                if (change.Item.UnlockChallengeId is int unlockChallengeId
                    && !_challenges.ValidateChallengeId(unlockChallengeId))
                {
                    return AdminSaveResult.Failure("Unlock challenge is invalid. A zone's unlock challenge must reference an existing challenge.");
                }
            }

            return ChangeSetProcessor.Apply(changes,
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
        }

        public AdminSaveResult SetEnemies(SetZoneEnemiesData data)
        {
            var zone = _zones.LookupZone(data.ZoneId);
            if (zone is null)
            {
                return AdminSaveResult.NotFound("Zone");
            }

            return ChildCollectionReconciler.Reconcile(
                existing: zone.ZoneEnemies,
                desired: data.ZoneEnemies,
                existingKey: ze => ze.EnemyId,
                desiredKey: ze => ze.EnemyId,
                delete: ze => _entityStore.Delete(new Entities.ZoneEnemy
                {
                    ZoneId = ze.ZoneId,
                    EnemyId = ze.EnemyId,
                }),
                insert: ze => _entityStore.Insert(new Entities.ZoneEnemy
                {
                    ZoneId = data.ZoneId,
                    EnemyId = ze.EnemyId,
                    Weight = ze.Weight,
                }),
                resourceName: "enemy",
                update: ze => _entityStore.Update(new Entities.ZoneEnemy
                {
                    ZoneId = data.ZoneId,
                    EnemyId = ze.EnemyId,
                    Weight = ze.Weight,
                }));
        }
    }
}
