using Game.Core.Attributes.Modifiers;

namespace Game.Core.Skills
{
    /// <summary>
    /// Represents a skill definition — immutable template data, not runtime battle state.
    /// </summary>
    public class Skill
    {
        public required int Id { get; set; }

        public required string Name { get; set; }

        public required double BaseDamage { get; set; }

        public required string Description { get; set; }

        public required int CooldownMs { get; set; }

        public required List<AttributeModifier> DamageMultipliers { get; set; }

        public required List<SkillEffect> Effects { get; set; }
    }
}
