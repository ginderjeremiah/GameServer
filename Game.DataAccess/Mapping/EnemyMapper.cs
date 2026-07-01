using Game.Core;
using Game.Core.Attributes;
using Game.Core.Enemies;
using Contracts = Game.Abstractions.Contracts;
using CoreSkill = Game.Core.Skills.Skill;
using EntityEnemy = Game.Infrastructure.Entities.Enemy;
using EntitySkill = Game.Infrastructure.Entities.Skill;
using EntityAttributeDistribution = Game.Infrastructure.Entities.AttributeDistribution;
using EntityEnemySkill = Game.Infrastructure.Entities.EnemySkill;
using EntityZoneEnemy = Game.Infrastructure.Entities.ZoneEnemy;

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
                DesignerNotes = entity.DesignerNotes,
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

        /// <summary>Maps a reference-data read <see cref="Contracts.Enemy"/> back to its entity graph (attribute
        /// distributions, skill-pool joins, and spawn-table joins) for the content seeder. The spawn rows are
        /// <c>ZoneEnemy</c> joins carried on the enemy; the seeder inserts them after both zones and enemies
        /// exist so the composite FK resolves.</summary>
        public static EntityEnemy ToEntity(Contracts.Enemy contract)
        {
            return new EntityEnemy
            {
                Id = contract.Id,
                Name = contract.Name,
                IsBoss = contract.IsBoss,
                DesignerNotes = contract.DesignerNotes,
                RetiredAt = contract.RetiredAt,
                AttributeDistributions = contract.AttributeDistribution
                    .Select(ad => new EntityAttributeDistribution
                    {
                        EnemyId = contract.Id,
                        AttributeId = (int)ad.AttributeId,
                        BaseAmount = ad.BaseAmount,
                        AmountPerLevel = ad.AmountPerLevel,
                    }).ToList(),
                EnemySkills = contract.SkillPool
                    .Select(skillId => new EntityEnemySkill
                    {
                        EnemyId = contract.Id,
                        SkillId = skillId,
                    }).ToList(),
                ZoneEnemies = contract.Spawns
                    .Select(spawn => new EntityZoneEnemy
                    {
                        ZoneId = spawn.ZoneId,
                        EnemyId = contract.Id,
                        Weight = spawn.Weight,
                    }).ToList(),
            };
        }

        /// <summary>
        /// Maps an entity <see cref="EntityEnemy"/> (with its child collections loaded) to a level-independent
        /// <see cref="EnemyTemplate"/>: the pre-mapped attribute distributions and available-skill loadout that
        /// a per-encounter <see cref="Enemy"/> is cloned from. Built once per snapshot so the gameplay reads
        /// (<c>GetDomainEnemy</c>/<c>GetRandomDomainEnemy</c>) reuse this graph rather than re-mapping it on
        /// every battle setup (#584). <paramref name="mappedSkills"/> is a build-scoped cache shared across all
        /// templates so each distinct skill is mapped to its immutable <see cref="CoreSkill"/> once, not once
        /// per (enemy, skill) pair; sharing the instance is safe because reference-data skills are immutable
        /// (docs/backend.md → Reference Data).
        /// </summary>
        public static EnemyTemplate ToTemplate(EntityEnemy entity, IReadOnlyList<EntitySkill> allSkills, Dictionary<int, CoreSkill> mappedSkills)
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
                AvailableSkills = entity.EnemySkills
                    .Select(es => ResolveSkill(entity, es.SkillId, allSkills, mappedSkills))
                    .ToList(),
            };
        }

        /// <summary>
        /// Resolves an enemy's available skill by direct index into the cached, zero-based, contiguous-id
        /// skill set (docs/backend.md → Reference Data), memoizing each distinct id into the build-scoped
        /// <paramref name="mappedSkills"/> cache so the same skill is mapped once and the immutable instance is
        /// shared across templates. A persisted <c>EnemySkill.SkillId</c> is FK-guaranteed in range against the
        /// (contiguity-asserted) skill set, so this always resolves; an out-of-range id can only be content-data
        /// corruption and fails loudly with a diagnosable message naming the enemy and offending id — mirroring
        /// the player-load loud-fail policy rather than the prior filter that silently dropped the skill.
        /// </summary>
        private static CoreSkill ResolveSkill(EntityEnemy enemy, int skillId, IReadOnlyList<EntitySkill> allSkills, Dictionary<int, CoreSkill> mappedSkills)
        {
            if (mappedSkills.TryGetValue(skillId, out var mapped))
            {
                return mapped;
            }

            if (skillId < 0 || skillId >= allSkills.Count)
            {
                throw new InvalidOperationException(
                    $"Enemy {enemy.Id} ('{enemy.Name}') references a skill with Id {skillId} that does not " +
                    "resolve against the skill catalog (a content-data mistake — a referenced id is out of range).");
            }

            mapped = SkillMapper.ToCore(allSkills[skillId]);
            mappedSkills[skillId] = mapped;
            return mapped;
        }
    }
}
