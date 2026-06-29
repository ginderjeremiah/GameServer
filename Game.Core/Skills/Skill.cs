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
        /// The leaf damage type this skill's direct hits deal (spike #1320). The damage pipeline resolves it
        /// to the attacker's amplification and defender's resistance attributes through
        /// <see cref="Attributes.DamageTypes.Applies"/>; an untyped pre-feature skill backfills to
        /// <see cref="EDamageType.Physical"/>, whose amp/resist attributes default to <c>0</c> (no behaviour
        /// change until typed content is authored).
        /// </summary>
        public required EDamageType DamageType { get; init; }

        /// <summary>
        /// The skill's rarity tier. The battle simulation never reads it, but the proficiency XP accrual does:
        /// at battle completion it maps the rarity to a tier weight (<see cref="Proficiencies.ProficiencyTierWeight"/>,
        /// #982/#1123) so a rarer fired skill pulls a larger share of the pie. See
        /// <see cref="Items.Item.Rarity"/> for the shared convention.
        /// </summary>
        public required ERarity Rarity { get; init; }

        public required IReadOnlyList<DamageMultiplier> DamageMultipliers { get; init; }

        public required IReadOnlyList<SkillEffect> Effects { get; init; }
    }
}
