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
            // must reference an existing challenge. Validate these record-set-specific references on every
            // Add/Edit up front so an invalid reference rejects the batch rather than partially applying.
            // (Edit-existence of the zone itself is the processor's shared editExists guard below.)
            foreach (var change in changes)
            {
                if (change.ChangeType == EChangeType.Delete)
                {
                    continue;
                }

                if (change.Item.BossEnemyId is int bossEnemyId
                    && _enemies.GetEnemy(bossEnemyId) is not { IsBoss: true })
                {
                    return AdminSaveResult.Failure("Boss enemy is invalid. A zone's boss must be an existing enemy marked as a boss.");
                }

                // The Home zone is a no-combat sanctuary: no enemies spawn there and never will. A boss is the
                // non-random enemy source, so reject one on a Home zone (the random spawn table is guarded
                // separately in SetEnemies / AdminEnemies.SetSpawns).
                if (change.Item.IsHome && change.Item.BossEnemyId is not null)
                {
                    return AdminSaveResult.Failure("The Home zone cannot have a boss. Home is a no-combat sanctuary where no enemies spawn.");
                }

                // SetEnemies / AdminEnemies.SetSpawns block populating a Home zone's spawn table going
                // forward, but neither fires against this save's own flip — an edit that sets IsHome on a
                // combat zone whose spawn table is already populated would otherwise leave the sanctuary with
                // live spawns. An Add can never carry pre-existing spawns, so this only applies to edits.
                if (change.ChangeType == EChangeType.Edit
                    && change.Item.IsHome
                    && _zones.LookupZone(change.Item.Id) is { ZoneEnemies.Count: > 0 })
                {
                    return AdminSaveResult.Failure("The Home zone cannot have enemy spawns. Home is a no-combat sanctuary where no enemies spawn; clear the zone's spawn table before making it Home.");
                }

                // Challenges are zero-based-id reference data, so a valid id is an in-range index (an O(1)
                // check, like the enemy/zone validators above).
                if (change.Item.UnlockChallengeId is int unlockChallengeId
                    && !_challenges.ValidateChallengeId(unlockChallengeId))
                {
                    return AdminSaveResult.Failure("Unlock challenge is invalid. A zone's unlock challenge must reference an existing challenge.");
                }
            }

            // The design mandates a single authored Home sanctuary, so reject any save whose resulting
            // catalogue would hold more than one non-retired Home zone (a second Home add, or an existing
            // combat zone flipped to Home). Backend-enforced like the boss/spawn-table Home guards above.
            if (WouldExceedSingleHomeZone(changes))
            {
                return AdminSaveResult.Failure(
                    "Only one Home zone is allowed. Another non-retired Home zone already exists; retire it first or make this a combat zone.");
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
                    IsHome = item.IsHome,
                    DesignerNotes = item.DesignerNotes,
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
                    IsHome = item.IsHome,
                    DesignerNotes = item.DesignerNotes,
                    RetiredAt = item.RetiredAt,
                }),
                key: item => item.Id,
                resourceName: "zone",
                // An edit must target an existing zone; a missing id is a not-found rejection (matching the
                // relationship setters), validated up front by the processor before anything is staged.
                editExists: item => _zones.LookupZone(item.Id) is not null);
        }

        /// <summary>
        /// True when applying <paramref name="changes"/> would leave more than one non-retired Home zone in
        /// the catalogue. The full post-save state is checked (cached zones folded with this batch's
        /// adds/edits/deletes), not just the changed item, so both adding a second Home zone and flipping an
        /// existing combat zone to Home are caught. Retired Home zones are out of circulation and don't count.
        /// </summary>
        private bool WouldExceedSingleHomeZone(IReadOnlyList<Change<Contracts.Zone>> changes)
        {
            // Existing zones this batch edits or deletes are superseded by their change; everything else keeps
            // its current cached Home/retirement state.
            var supersededIds = changes
                .Where(change => change.ChangeType != EChangeType.Add)
                .Select(change => change.Item.Id)
                .ToHashSet();

            var homeCount = _zones.AllZones()
                .Count(zone => !supersededIds.Contains(zone.Id) && IsActiveHome(zone.IsHome, zone.RetiredAt));

            // Adds and edits contribute their post-save state (a delete contributes nothing).
            homeCount += changes
                .Where(change => change.ChangeType != EChangeType.Delete)
                .Count(change => IsActiveHome(change.Item.IsHome, change.Item.RetiredAt));

            return homeCount > 1;
        }

        private static bool IsActiveHome(bool isHome, DateTime? retiredAt)
        {
            return isHome && retiredAt is null;
        }

        public AdminSaveResult SetEnemies(SetZoneEnemiesData data)
        {
            var zone = _zones.LookupZone(data.ZoneId);
            if (zone is null)
            {
                return AdminSaveResult.NotFound("Zone");
            }

            // Anti-tamper: a negative weight commits cleanly but throws inside ProbabilityTable's
            // constructor when the enemy snapshot next rebuilds, permanently poisoning every instance's
            // reload (and boot) since the Workbench's own corrective save then collides with the already-
            // committed row. Reject it up front. Zero is safe — ProbabilityTable treats an all-zero set
            // as uniform.
            if (data.ZoneEnemies.Any(ze => ze.Weight < 0))
            {
                return AdminSaveResult.Failure("A zone enemy's spawn weight cannot be negative.");
            }

            // The Home zone is a no-combat sanctuary: no enemies spawn there and never will. Reject assigning
            // a spawn table to it (clearing it to empty stays allowed). Mirrors the per-enemy guard in
            // AdminEnemies.SetSpawns so neither authoring direction can populate Home's spawn table.
            if (zone.IsHome && data.ZoneEnemies.Count > 0)
            {
                return AdminSaveResult.Failure("The Home zone cannot have enemy spawns. Home is a no-combat sanctuary where no enemies spawn.");
            }

            // A desired spawn must reference an existing enemy; otherwise the insert FK-faults at commit
            // instead of rejecting gracefully. Mirrors AdminEnemies.SetSkills' up-front skill-existence check.
            foreach (var zoneEnemy in data.ZoneEnemies)
            {
                if (_enemies.GetEnemy(zoneEnemy.EnemyId) is null)
                {
                    return AdminSaveResult.Failure($"Enemy {zoneEnemy.EnemyId} does not exist.");
                }
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
