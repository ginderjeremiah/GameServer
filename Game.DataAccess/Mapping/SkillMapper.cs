using Game.Core;
using Game.Core.Attributes;
using Game.Core.Attributes.Modifiers;
using Game.Core.Skills;
using EntitySkill = Game.Abstractions.Entities.Skill;

namespace Game.DataAccess.Mapping
{
    internal static class SkillMapper
    {
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
