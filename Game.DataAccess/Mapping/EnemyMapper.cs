using Game.Core;
using Game.Core.Attributes;
using Game.Core.Enemies;
using EntityEnemy = Game.Abstractions.Entities.Enemy;
using EntitySkill = Game.Abstractions.Entities.Skill;

namespace Game.DataAccess.Mapping
{
    internal static class EnemyMapper
    {
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
                Skills = (entity.EnemySkills ?? [])
                    .Where(es => skillLookup.ContainsKey(es.SkillId))
                    .Select(es => SkillMapper.ToCore(skillLookup[es.SkillId]))
                    .ToList(),
            };
        }
    }
}
