namespace Game.Core.Skills
{
    /// <summary>
    /// Represents a skill definition — immutable template data, not runtime battle state. Shared, cached
    /// reference-data instance: structurally immutable (init-only properties, read-only collections) so the
    /// cached instance returned to every player cannot be corrupted (#547). Per-battle runtime state
    /// (charge time) lives on the <see cref="Battle.BattleSkill"/> wrapper, not here.
    /// </summary>
    public class Skill
    {
        public required int Id { get; init; }

        public required string Name { get; init; }

        public required double BaseDamage { get; init; }

        public required string Description { get; init; }

        public required int CooldownMs { get; init; }

        /// <summary>
        /// The skill's rarity tier. Authoring/display metadata today (the battle never reads it), but a
        /// reserved logic hook: it is the tier weight the proficiency XP accrual reads by id at battle
        /// completion (#982/#1123). See <see cref="Items.Item.Rarity"/> for the shared convention.
        /// </summary>
        public required ERarity Rarity { get; init; }

        public required IReadOnlyList<DamageMultiplier> DamageMultipliers { get; init; }

        public required IReadOnlyList<SkillEffect> Effects { get; init; }
    }
}
