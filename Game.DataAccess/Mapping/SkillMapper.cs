using Game.Core;
using Game.Core.Attributes;
using Game.Core.Skills;
using Contracts = Game.Abstractions.Contracts;
using EntitySkill = Game.Infrastructure.Entities.Skill;
using CoreSkillEffect = Game.Core.Skills.SkillEffect;

namespace Game.DataAccess.Mapping
{
    internal static class SkillMapper
    {
        /// <summary>Maps an entity <see cref="EntitySkill"/> (with its damage multipliers and effects loaded)
        /// to the reference-data read <see cref="Contracts.Skill"/> contract.</summary>
        public static Contracts.Skill ToContract(EntitySkill entity)
        {
            return new Contracts.Skill
            {
                Id = entity.Id,
                Name = entity.Name,
                BaseDamage = entity.BaseDamage,
                Description = entity.Description,
                CooldownMs = entity.CooldownMs,
                IconPath = entity.IconPath,
                DamageMultipliers = entity.SkillDamageMultipliers
                    .Select(sdm => new Contracts.AttributeMultiplier
                    {
                        AttributeId = (EAttribute)sdm.AttributeId,
                        Multiplier = sdm.Multiplier,
                    }).ToList(),
                Effects = entity.SkillEffects
                    .Select(se => new Contracts.SkillEffect
                    {
                        Id = se.Id,
                        Target = (ESkillEffectTarget)se.Target,
                        AttributeId = (EAttribute)se.AttributeId,
                        ModifierTypeId = (EModifierType)se.ModifierType,
                        Amount = se.Amount,
                        DurationMs = se.DurationMs,
                        ScalingAttributeId = (EAttribute)se.ScalingAttributeId,
                        ScalingAmount = se.ScalingAmount,
                    }).ToList(),
                RetiredAt = entity.RetiredAt,
            };
        }

        /// <summary>
        /// Maps an entity <see cref="EntitySkill"/> (with its damage multipliers and effects loaded) to a
        /// domain <see cref="Skill"/>.
        /// </summary>
        public static Skill ToCore(EntitySkill entity)
        {
            return new Skill
            {
                Id = entity.Id,
                Name = entity.Name,
                BaseDamage = (double)entity.BaseDamage,
                Description = entity.Description,
                CooldownMs = entity.CooldownMs,
                DamageMultipliers = entity.SkillDamageMultipliers
                    .Select(sdm => new DamageMultiplier
                    {
                        Attribute = (EAttribute)sdm.AttributeId,
                        Amount = (double)sdm.Multiplier,
                    }).ToList(),
                Effects = entity.SkillEffects
                    .Select(se => new CoreSkillEffect
                    {
                        Id = se.Id,
                        Target = (ESkillEffectTarget)se.Target,
                        AttributeId = (EAttribute)se.AttributeId,
                        ModifierType = (EModifierType)se.ModifierType,
                        Amount = (double)se.Amount,
                        DurationMs = se.DurationMs,
                        ScalingAttributeId = (EAttribute)se.ScalingAttributeId,
                        ScalingAmount = (double)se.ScalingAmount,
                    }).ToList(),
            };
        }
    }
}
