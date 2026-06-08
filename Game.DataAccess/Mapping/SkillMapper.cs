using Game.Core;
using Game.Core.Attributes;
using Game.Core.Attributes.Modifiers;
using Game.Core.Skills;
using Contracts = Game.Abstractions.Contracts;
using EntitySkill = Game.Infrastructure.Entities.Skill;

namespace Game.DataAccess.Mapping
{
    internal static class SkillMapper
    {
        /// <summary>Maps an entity <see cref="EntitySkill"/> (with its damage multipliers loaded) to the
        /// reference-data read <see cref="Contracts.Skill"/> contract.</summary>
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
            };
        }

        /// <summary>
        /// Maps an entity <see cref="EntitySkill"/> (with its damage multipliers loaded) to a
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
                DamageMultipliers = (entity.SkillDamageMultipliers ?? [])
                    .Select(sdm => new AttributeModifier
                    {
                        Attribute = (EAttribute)sdm.AttributeId,
                        Amount = (double)sdm.Multiplier,
                        Type = EModifierType.Multiplicative,
                        Source = EAttributeModifierSource.Derived,
                    }).ToList(),
            };
        }
    }
}
