using Game.Abstractions.Contracts.Admin;
using Game.Abstractions.DataAccess.Admin;
using Game.Core;
using Contracts = Game.Abstractions.Contracts;
using Entities = Game.Infrastructure.Entities;

namespace Game.DataAccess.Repositories.Admin
{
    /// <summary>
    /// Content Authoring persistence for enemies. Reuses the cached entity lookups
    /// (<see cref="IEnemyEntityCache.GetEnemy"/>, <see cref="IZoneEntityCache.LookupZone"/>) for
    /// existence/diff and builds fresh, navigation-free entities for every write so a cached
    /// <c>Include(...)</c> graph is never dragged into the change tracker. Changes are staged on the unit
    /// of work; the per-action commit filter persists them.
    /// </summary>
    internal class AdminEnemies(IEnemyEntityCache enemies, IZoneEntityCache zones, ISkillEntityCache skills, IEntityStore entityStore) : IAdminEnemies
    {
        private readonly IEnemyEntityCache _enemies = enemies;
        private readonly IZoneEntityCache _zones = zones;
        private readonly ISkillEntityCache _skills = skills;
        private readonly IEntityStore _entityStore = entityStore;

        public AdminSaveResult SaveEnemies(IReadOnlyList<Change<Contracts.Enemy>> changes)
        {
            // Authoring guard: retiring an enemy must not strip a live (non-retired) zone of its last
            // spawnable enemy — that would drop the zone from the random spawn tables and throw at the next
            // idle encounter. The runtime relocation safety net only rescues an occupant of an already-retired
            // or empty zone, so the live-zone case is rejected at save time; the workflow is to retire the
            // zone first, then its enemies (see docs/backend.md → Retiring reference data).
            if (FindLiveZoneLeftEmpty(changes) is { } rejection)
            {
                return rejection;
            }

            return ChangeSetProcessor.Apply(changes,
                add: item => _entityStore.Insert(new Entities.Enemy
                {
                    Name = item.Name,
                    IsBoss = item.IsBoss,
                    DesignerNotes = item.DesignerNotes,
                }),
                edit: item => _entityStore.Update(new Entities.Enemy
                {
                    Id = item.Id,
                    Name = item.Name,
                    IsBoss = item.IsBoss,
                    DesignerNotes = item.DesignerNotes,
                    RetiredAt = item.RetiredAt,
                }),
                key: item => item.Id,
                resourceName: "enemy",
                // An edit must target an existing enemy; a missing id is a not-found rejection (matching the
                // relationship setters), validated up front by the processor before anything is staged.
                editExists: item => _enemies.GetEnemy(item.Id) is not null);
        }

        public AdminSaveResult SetAttributeDistributions(SetEnemyAttributeDistributions data)
        {
            var enemy = _enemies.GetEnemy(data.EnemyId);
            if (enemy is null)
            {
                return AdminSaveResult.NotFound("Enemy");
            }

            return ChildCollectionReconciler.Reconcile(
                existing: enemy.AttributeDistributions,
                desired: data.AttributeDistributions,
                existingKey: ad => ad.AttributeId,
                desiredKey: ad => (int)ad.AttributeId,
                delete: ad => _entityStore.Delete(new Entities.AttributeDistribution
                {
                    EnemyId = enemy.Id,
                    AttributeId = ad.AttributeId,
                }),
                insert: ad => _entityStore.Insert(new Entities.AttributeDistribution
                {
                    EnemyId = enemy.Id,
                    AttributeId = (int)ad.AttributeId,
                    BaseAmount = ad.BaseAmount,
                    AmountPerLevel = ad.AmountPerLevel,
                }),
                resourceName: "attribute distribution",
                update: ad => _entityStore.Update(new Entities.AttributeDistribution
                {
                    EnemyId = enemy.Id,
                    AttributeId = (int)ad.AttributeId,
                    BaseAmount = ad.BaseAmount,
                    AmountPerLevel = ad.AmountPerLevel,
                }));
        }

        public AdminSaveResult SetSkills(SetEnemySkillsData data)
        {
            var enemy = _enemies.GetEnemy(data.EnemyId);
            if (enemy is null)
            {
                return AdminSaveResult.NotFound("Enemy");
            }

            // Authoring guard (anti-tamper): every skill assigned to an enemy must declare itself
            // Enemy-acquirable. The flag is the declared intent; this pool is the reality, so the save
            // bridges them — rejected up front (a tampered admin client can't bypass the frontend's
            // filtered picker). The whole desired set is checked, so a skill whose flag was later cleared
            // can no longer be re-saved onto the enemy.
            if (FindEnemySkillFlagViolation(data.SkillIds) is { } rejection)
            {
                return rejection;
            }

            // An EnemySkill is a pure join row (no payload beyond its key), so a skill present on both
            // sides needs no update — only deletes and inserts apply.
            return ChildCollectionReconciler.Reconcile(
                existing: enemy.EnemySkills,
                desired: data.SkillIds,
                existingKey: es => es.SkillId,
                desiredKey: id => id,
                delete: es => _entityStore.Delete(new Entities.EnemySkill
                {
                    EnemyId = enemy.Id,
                    SkillId = es.SkillId,
                }),
                insert: id => _entityStore.Insert(new Entities.EnemySkill
                {
                    EnemyId = enemy.Id,
                    SkillId = id,
                }),
                resourceName: "skill");
        }

        public AdminSaveResult SetSpawns(SetEnemySpawnsData data)
        {
            var enemy = _enemies.GetEnemy(data.EnemyId);
            if (enemy is null)
            {
                return AdminSaveResult.NotFound("Enemy");
            }

            // Authoring guard: the Home zone is a no-combat sanctuary where no enemies spawn (and never will),
            // so reject a spawn that targets one. Mirrors the per-zone guard in AdminZones.SetEnemies so neither
            // authoring direction can populate Home's spawn table.
            foreach (var spawn in data.Spawns)
            {
                if (_zones.LookupZone(spawn.ZoneId) is { IsHome: true } homeZone)
                {
                    return AdminSaveResult.Failure(
                        $"'{enemy.Name}' cannot spawn in the Home zone ('{homeZone.Name}'). Home is a no-combat sanctuary where no enemies spawn.");
                }
            }

            return ChildCollectionReconciler.Reconcile(
                existing: enemy.ZoneEnemies,
                desired: data.Spawns,
                existingKey: ze => ze.ZoneId,
                desiredKey: s => s.ZoneId,
                delete: ze => _entityStore.Delete(new Entities.ZoneEnemy
                {
                    ZoneId = ze.ZoneId,
                    EnemyId = enemy.Id,
                }),
                insert: s => _entityStore.Insert(new Entities.ZoneEnemy
                {
                    ZoneId = s.ZoneId,
                    EnemyId = enemy.Id,
                    Weight = s.Weight,
                }),
                resourceName: "spawn",
                update: s => _entityStore.Update(new Entities.ZoneEnemy
                {
                    ZoneId = s.ZoneId,
                    EnemyId = enemy.Id,
                    Weight = s.Weight,
                }));
        }

        /// <summary>
        /// Returns a rejection for the first desired skill that is not <see cref="ESkillAcquisition.Enemy"/>-flagged
        /// (or does not exist), or null when every assigned skill is valid.
        /// </summary>
        private AdminSaveResult? FindEnemySkillFlagViolation(IEnumerable<int> skillIds)
        {
            foreach (var skillId in skillIds)
            {
                var skill = _skills.LookupSkill(skillId);
                if (skill is null)
                {
                    return AdminSaveResult.Failure($"Skill {skillId} does not exist.");
                }

                if (!((ESkillAcquisition)skill.Acquisition).HasFlag(ESkillAcquisition.Enemy))
                {
                    return AdminSaveResult.Failure(
                        $"Skill '{skill.Name}' is not flagged as an Enemy skill and cannot be assigned to an enemy.");
                }
            }

            return null;
        }

        /// <summary>
        /// Detects whether applying these enemy edits would leave a live (non-retired) zone with no
        /// spawnable enemies, returning the user-facing rejection for the first such zone (or null when the
        /// save is safe). Only the retirement transition matters: an enemy outside the batch keeps its
        /// current cached retirement, so the check combines the batch's edits with the cached state.
        /// </summary>
        private AdminSaveResult? FindLiveZoneLeftEmpty(IReadOnlyList<Change<Contracts.Enemy>> changes)
        {
            // A malformed batch naming the same key more than once is rejected by the processor's shared
            // duplicate guard below; skip the viability check (its per-id map can't represent a duplicated
            // key) and let that rejection stand rather than throwing here.
            if (ChangeSetProcessor.HasDuplicateKey(changes, change => change.ChangeType != EChangeType.Add, item => item.Id))
            {
                return null;
            }

            // Post-save retirement for each enemy this batch edits. Built once so the per-zone checks are
            // pure lookups (an enemy not in the batch keeps its current cached state).
            var editedRetirement = changes
                .Where(change => change.ChangeType == EChangeType.Edit)
                .ToDictionary(change => change.Item.Id, change => change.Item.RetiredAt is not null);

            // No edit retires an enemy, so nothing this save does can empty a zone.
            if (!editedRetirement.Values.Any(retired => retired))
            {
                return null;
            }

            bool WillBeRetired(int enemyId)
            {
                return editedRetirement.TryGetValue(enemyId, out var retired)
                    ? retired
                    : _enemies.GetEnemy(enemyId) is { RetiredAt: not null };
            }

            // Only zones an about-to-be-retired enemy currently spawns in can be affected; their spawn
            // memberships are eager-loaded on the cached enemy entity.
            var affectedZoneIds = editedRetirement
                .Where(retirement => retirement.Value)
                .Select(retirement => _enemies.GetEnemy(retirement.Key))
                .OfType<Entities.Enemy>()
                .SelectMany(enemy => enemy.ZoneEnemies.Select(zoneEnemy => zoneEnemy.ZoneId))
                .ToHashSet();

            foreach (var zoneId in affectedZoneIds)
            {
                var zone = _zones.LookupZone(zoneId);
                // A missing or already-retired zone is not a live zone: the runtime relocation safety net
                // covers an occupant, so retiring its enemies is allowed.
                if (zone is null || zone.RetiredAt is not null)
                {
                    continue;
                }

                var spawnerIds = zone.ZoneEnemies.Select(zoneEnemy => zoneEnemy.EnemyId).ToList();
                var wasViable = spawnerIds.Any(id => _enemies.GetEnemy(id) is { RetiredAt: null });
                var staysViable = spawnerIds.Any(id => !WillBeRetired(id));

                // Reject only when this save is what empties a currently-viable live zone; a zone already
                // empty before the save is left to the runtime safety net rather than blamed on this edit.
                if (wasViable && !staysViable)
                {
                    return AdminSaveResult.Failure(LastSpawnRejection(zone, spawnerIds));
                }
            }

            return null;
        }

        /// <summary>
        /// The rejection message for a retire that would leave <paramref name="zone"/> with no spawnable
        /// enemies, naming the zone and the enemies being retired out of it (its currently-active spawners,
        /// which are exactly the ones this save retires).
        /// </summary>
        private string LastSpawnRejection(Entities.Zone zone, IEnumerable<int> spawnerIds)
        {
            var retiredNames = spawnerIds
                .Select(_enemies.GetEnemy)
                .OfType<Entities.Enemy>()
                .Where(enemy => enemy.RetiredAt is null)
                .Select(enemy => $"'{enemy.Name}'");

            return $"Retiring {string.Join(", ", retiredNames)} would leave live zone '{zone.Name}' with no "
                + "spawnable enemies. Retire the zone first, or keep at least one active enemy spawning in it.";
        }
    }
}
