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
        /// The skill's rarity tier — display/authoring metadata only. Neither the battle simulation nor (as of
        /// the effect-based proficiency accrual, spike #1318) the XP path reads it: rarity reverts to
        /// display-only, surfaced from the skills contract, since damage already reflects a skill's power and an
        /// explicit rarity weight would double-count. Retained on the lean model only pending its removal (the
        /// accrual was its sole reader — follow-up cleanup). See <see cref="Items.Item.Rarity"/> for the shared
        /// convention.
        /// </summary>
        public required ERarity Rarity { get; init; }

        public required IReadOnlyList<DamageMultiplier> DamageMultipliers { get; init; }

        public required IReadOnlyList<SkillEffect> Effects { get; init; }
    }
}
