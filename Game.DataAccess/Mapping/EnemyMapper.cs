using Game.Core;
using Game.Core.Attributes;
using Game.Core.Enemies;
using Contracts = Game.Abstractions.Contracts;
using EntityEnemy = Game.Infrastructure.Entities.Enemy;
using EntitySkill = Game.Infrastructure.Entities.Skill;

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
                RetiredAt = entity.RetiredAt,
            };
        }

        /// <summary>
        /// Maps an entity <see cref="EntityEnemy"/> (with its child collections loaded) to a level-independent
        /// <see cref="EnemyTemplate"/>: the pre-mapped attribute distributions and available-skill loadout that
        /// a per-encounter <see cref="Enemy"/> is cloned from. Built once per snapshot so the gameplay reads
        /// (<c>GetDomainEnemy</c>/<c>GetRandomDomainEnemy</c>) reuse this graph rather than re-mapping it on
        /// every battle setup (#584).
        /// </summary>
        public static EnemyTemplate ToTemplate(EntityEnemy entity, IReadOnlyList<EntitySkill> allSkills)
        {
            return new EnemyTemplate
            {
                Id = entity.Id,
                Name = entity.Name,
                IsBoss = entity.IsBoss,
                AttributeDistributions = entity.AttributeDistributions
                    .Select(ad => new AttributeDistribution
                    {
                        AttributeId = (EAttribute)ad.AttributeId,
                        BaseAmount = ad.BaseAmount,
                        AmountPerLevel = ad.AmountPerLevel,
                    }).ToList(),
                // The cached skill list is the zero-based, contiguous-id reference set (docs/backend.md
                // → Reference Data), so a skill resolves by direct index instead of a per-call dictionary.
                // The bounds check skips a malformed out-of-range ref, matching the prior skip-if-missing
                // behaviour (a persisted EnemySkill.SkillId is FK-guaranteed in range, so it is defensive).
                AvailableSkills = entity.EnemySkills
                    .Where(es => es.SkillId >= 0 && es.SkillId < allSkills.Count)
                    .Select(es => SkillMapper.ToCore(allSkills[es.SkillId]))
                    .ToList(),
            };
        }
    }
}
