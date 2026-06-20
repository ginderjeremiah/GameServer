using Game.Abstractions.Contracts.Admin;
using Game.Abstractions.DataAccess.Admin;
using Contracts = Game.Abstractions.Contracts;
using Entities = Game.Infrastructure.Entities;

namespace Game.DataAccess.Repositories.Admin
{
    /// <summary>
    /// Content Authoring persistence for enemies. Reuses the cached entity lookup
    /// (<see cref="IEnemyEntityCache.GetEnemy"/>) for existence/diff and builds fresh, navigation-free
    /// entities for every write so a cached <c>Include(...)</c> graph is never dragged into the
    /// change tracker. Changes are staged on the unit of work; the per-action commit filter persists them.
    /// </summary>
    internal class AdminEnemies(IEnemyEntityCache enemies, IEntityStore entityStore) : IAdminEnemies
    {
        private readonly IEnemyEntityCache _enemies = enemies;
        private readonly IEntityStore _entityStore = entityStore;

        public AdminSaveResult SaveEnemies(IReadOnlyList<Change<Contracts.Enemy>> changes)
        {
            // An edit must target an existing enemy; a missing id is a not-found rejection (matching the
            // relationship setters), not an EF 0-row update that throws. Validate the whole batch up front
            // so the commit filter doesn't persist the rest of the batch alongside an invalid edit.
            if (changes.Any(c => c.ChangeType == EChangeType.Edit && _enemies.GetEnemy(c.Item.Id) is null))
            {
                return AdminSaveResult.NotFound("Enemy");
            }

            return ChangeSetProcessor.Apply(changes,
                add: item => _entityStore.Insert(new Entities.Enemy
                {
                    Name = item.Name,
                    IsBoss = item.IsBoss,
                }),
                edit: item => _entityStore.Update(new Entities.Enemy
                {
                    Id = item.Id,
                    Name = item.Name,
                    IsBoss = item.IsBoss,
                    RetiredAt = item.RetiredAt,
                }),
                key: item => item.Id,
                resourceName: "enemy");
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
    }
}
