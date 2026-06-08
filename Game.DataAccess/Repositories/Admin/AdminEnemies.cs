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

        public void SaveEnemies(IReadOnlyList<Change<Contracts.Enemy>> changes)
        {
            ChangeSetProcessor.Apply(changes,
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
                }),
                delete: item => _entityStore.Delete(new Entities.Enemy
                {
                    Id = item.Id,
                    Name = "",
                }));
        }

        public bool SetAttributeDistributions(SetEnemyAttributeDistributions data)
        {
            var enemy = _enemies.GetEnemy(data.EnemyId);
            if (enemy is null)
            {
                return false;
            }

            var newIds = data.AttributeDistributions.Select(ad => (int)ad.AttributeId).ToList();
            foreach (var dist in enemy.AttributeDistributions.Where(ad => !newIds.Contains(ad.AttributeId)))
            {
                _entityStore.Delete(new Entities.AttributeDistribution
                {
                    EnemyId = enemy.Id,
                    AttributeId = dist.AttributeId,
                });
            }

            foreach (var dist in enemy.AttributeDistributions.Where(ad => newIds.Contains(ad.AttributeId)))
            {
                var newData = data.AttributeDistributions.First(ad => (int)ad.AttributeId == dist.AttributeId);
                _entityStore.Update(new Entities.AttributeDistribution
                {
                    EnemyId = enemy.Id,
                    AttributeId = dist.AttributeId,
                    BaseAmount = newData.BaseAmount,
                    AmountPerLevel = newData.AmountPerLevel
                });
            }

            var existingIds = enemy.AttributeDistributions.Select(ad => ad.AttributeId).ToList();
            var newDistributions = data.AttributeDistributions
                .Where(ad => !existingIds.Contains((int)ad.AttributeId))
                .Select(ad => new Entities.AttributeDistribution
                {
                    EnemyId = enemy.Id,
                    AttributeId = (int)ad.AttributeId,
                    BaseAmount = ad.BaseAmount,
                    AmountPerLevel = ad.AmountPerLevel
                }).ToList();

            _entityStore.InsertAll(newDistributions);
            return true;
        }

        public bool SetSkills(SetEnemySkillsData data)
        {
            var enemy = _enemies.GetEnemy(data.EnemyId);
            if (enemy is null)
            {
                return false;
            }

            var newIds = data.SkillIds;
            foreach (var skill in enemy.EnemySkills.Where(e => !newIds.Contains(e.SkillId)))
            {
                _entityStore.Delete(new Entities.EnemySkill
                {
                    EnemyId = enemy.Id,
                    SkillId = skill.SkillId,
                });
            }

            var existingIds = enemy.EnemySkills.Select(es => es.SkillId).ToList();
            var enemySkills = data.SkillIds
                .Where(id => !existingIds.Contains(id))
                .Select(id => new Entities.EnemySkill
                {
                    EnemyId = enemy.Id,
                    SkillId = id,
                }).ToList();

            _entityStore.InsertAll(enemySkills);
            return true;
        }

        public bool SetSpawns(SetEnemySpawnsData data)
        {
            var enemy = _enemies.GetEnemy(data.EnemyId);
            if (enemy is null)
            {
                return false;
            }

            var newZoneIds = data.Spawns.Select(s => s.ZoneId).ToList();
            foreach (var spawn in enemy.ZoneEnemies.Where(ze => !newZoneIds.Contains(ze.ZoneId)))
            {
                _entityStore.Delete(new Entities.ZoneEnemy
                {
                    ZoneId = spawn.ZoneId,
                    EnemyId = enemy.Id,
                });
            }

            foreach (var spawn in enemy.ZoneEnemies.Where(ze => newZoneIds.Contains(ze.ZoneId)))
            {
                var newData = data.Spawns.First(s => s.ZoneId == spawn.ZoneId);
                _entityStore.Update(new Entities.ZoneEnemy
                {
                    ZoneId = spawn.ZoneId,
                    EnemyId = enemy.Id,
                    Weight = newData.Weight,
                });
            }

            var existingZoneIds = enemy.ZoneEnemies.Select(ze => ze.ZoneId).ToList();
            var newSpawns = data.Spawns
                .Where(s => !existingZoneIds.Contains(s.ZoneId))
                .Select(s => new Entities.ZoneEnemy
                {
                    ZoneId = s.ZoneId,
                    EnemyId = enemy.Id,
                    Weight = s.Weight,
                }).ToList();

            _entityStore.InsertAll(newSpawns);
            return true;
        }
    }
}
