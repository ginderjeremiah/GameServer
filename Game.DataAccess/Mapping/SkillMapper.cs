using Game.Core;
using Game.Core.Attributes;
using Game.Core.Skills;
using Contracts = Game.Abstractions.Contracts;
using EntitySkill = Game.Infrastructure.Entities.Skill;
using EntitySkillDamagePortion = Game.Infrastructure.Entities.SkillDamagePortion;
using EntitySkillDamageMultiplier = Game.Infrastructure.Entities.SkillDamageMultiplier;
using EntitySkillEffect = Game.Infrastructure.Entities.SkillEffect;
using CoreSkillEffect = Game.Core.Skills.SkillEffect;

namespace Game.DataAccess.Mapping
{
    internal static class SkillMapper
    {
        /// <summary>Maps an entity <see cref="EntitySkill"/> (with its damage portions, damage multipliers and
        /// effects loaded) to the reference-data read <see cref="Contracts.Skill"/> contract.</summary>
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
                RarityId = (ERarity)entity.RarityId,
                Word = entity.Word,
                Pronunciation = entity.Pronunciation,
                Translation = entity.Translation,
                Acquisition = (ESkillAcquisition)entity.Acquisition,
                DesignerNotes = entity.DesignerNotes,
                DamagePortions = entity.SkillDamagePortions
                    .Select(p => new Contracts.SkillDamagePortion
                    {
                        Type = (EDamageType)p.DamageType,
                        Weight = p.Weight,
                    }).ToList(),
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

        /// <summary>Maps a reference-data read <see cref="Contracts.Skill"/> back to its entity graph (with its
        /// damage portions, damage multipliers and effects), for the content seeder that reconstructs the static
        /// content from the source-controlled export. The child rows carry the parent id explicitly so the
        /// bulk seeder can insert them without change-tracking fixups.</summary>
        public static EntitySkill ToEntity(Contracts.Skill contract)
        {
            return new EntitySkill
            {
                Id = contract.Id,
                Name = contract.Name,
                BaseDamage = contract.BaseDamage,
                Description = contract.Description,
                CooldownMs = contract.CooldownMs,
                IconPath = contract.IconPath,
                RarityId = (int)contract.RarityId,
                Word = contract.Word,
                Pronunciation = contract.Pronunciation,
                Translation = contract.Translation,
                Acquisition = (int)contract.Acquisition,
                DesignerNotes = contract.DesignerNotes,
                RetiredAt = contract.RetiredAt,
                SkillDamagePortions = contract.DamagePortions
                    .Select(p => new EntitySkillDamagePortion
                    {
                        SkillId = contract.Id,
                        DamageType = (int)p.Type,
                        Weight = p.Weight,
                    }).ToList(),
                SkillDamageMultipliers = contract.DamageMultipliers
                    .Select(m => new EntitySkillDamageMultiplier
                    {
                        SkillId = contract.Id,
                        AttributeId = (int)m.AttributeId,
                        Multiplier = m.Multiplier,
                    }).ToList(),
                SkillEffects = contract.Effects
                    .Select(e => new EntitySkillEffect
                    {
                        Id = e.Id,
                        SkillId = contract.Id,
                        Target = (int)e.Target,
                        AttributeId = (int)e.AttributeId,
                        ModifierType = (int)e.ModifierTypeId,
                        Amount = e.Amount,
                        DurationMs = e.DurationMs,
                        ScalingAttributeId = (int)e.ScalingAttributeId,
                        ScalingAmount = e.ScalingAmount,
                    }).ToList(),
            };
        }

        /// <summary>
        /// Maps an entity <see cref="EntitySkill"/> (with its damage portions, damage multipliers and effects
        /// loaded) to a domain <see cref="Skill"/>.
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
                DamagePortions = entity.SkillDamagePortions
                    .Select(p => new SkillDamagePortion
                    {
                        Type = (EDamageType)p.DamageType,
                        Weight = (double)p.Weight,
                    }).ToList(),
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
