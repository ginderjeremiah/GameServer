using Game.Core;
using Game.Core.Attributes;
using Game.Core.Enemies;
using Contracts = Game.Abstractions.Contracts;
using EntityEnemy = Game.Abstractions.Entities.Enemy;
using EntitySkill = Game.Abstractions.Entities.Skill;

namespace Game.DataAccess.Mapping
{
    internal static class EnemyMapper
    {
        /// <summary>Maps an entity <see cref="EntityEnemy"/> (with its child collections loaded) to the
        /// reference-data read <see cref="Contracts.Enemy"/> contract.</summary>
        public static Contracts.Enemy ToContract(EntityEnemy entity)
        {
            return new Contracts.Enemy
            {
                Id = entity.Id,
                Name = entity.Name,
                IsBoss = entity.IsBoss,
                AttributeDistribution = entity.AttributeDistributions
                    .Select(ad => new Contracts.AttributeDistribution
                    {
                        AttributeId = (EAttribute)ad.AttributeId,
                        BaseAmount = ad.BaseAmount,
                        AmountPerLevel = ad.AmountPerLevel,
                    }).ToList(),
                SkillPool = entity.EnemySkills.Select(es => es.SkillId).ToList(),
                Spawns = entity.ZoneEnemies
                    .Select(ze => new Contracts.EnemySpawn
                    {
                        ZoneId = ze.ZoneId,
                        Weight = ze.Weight,
                    }).ToList(),
            };
        }

        public static Enemy ToCore(
            EntityEnemy entity,
            int level,
            IReadOnlyList<EntitySkill> allSkills)
        {
            var skillLookup = allSkills.ToDictionary(s => s.Id);

            return new Enemy
            {
                Id = entity.Id,
                Name = entity.Name,
                IsBoss = entity.IsBoss,
                Level = level,
                AttributeDistributions = (entity.AttributeDistributions ?? [])
                    .Select(ad => new AttributeDistribution
                    {
                        AttributeId = (EAttribute)ad.AttributeId,
                        BaseAmount = ad.BaseAmount,
                        AmountPerLevel = ad.AmountPerLevel,
                    }).ToList(),
                AvailableSkills = (entity.EnemySkills ?? [])
                    .Where(es => skillLookup.ContainsKey(es.SkillId))
                    .Select(es => SkillMapper.ToCore(skillLookup[es.SkillId]))
                    .ToList(),
            };
        }
    }
}
