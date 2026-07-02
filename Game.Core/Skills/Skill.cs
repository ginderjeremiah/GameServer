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
        /// This skill's own base critical-hit chance — a decimal probability (0.05 = 5%) compared, after
        /// scaling by the attacking battler's <see cref="EAttribute.CriticalChanceMultiplier"/>, directly
        /// against the battle RNG draw (<see cref="Battle.BattleContext.DamageTarget"/>). The per-skill opt-in
        /// enabler the crit rework (#1425) anticipated but never wired up (#1453): <c>0</c> by default, so an
        /// unauthored skill never crits regardless of the multiplier, making crit a committed identity of the
        /// specific skill rather than a build-wide stat.
        /// </summary>
        public required double CriticalChance { get; init; }

        /// <summary>
        /// The weighted leaf-type split this skill's direct hits deal (spike #1343). A hit's raw damage is
        /// split across these portions by weight; each portion runs the single-type pipeline under its own
        /// type. A single-portion skill is byte-for-byte the pre-feature single-typed hit. The direct-hit
        /// pipeline (<see cref="Battle.BattleContext.DamageTarget"/>) reads these portions (#1385);
        /// <see cref="PrimaryDamageType"/> feeds only the display surfaces (icon/colour). <b>Invariant:</b>
        /// every skill carries at least one positive-weight portion (authoring validation requires it, and
        /// existing skills were backfilled to <c>[{ Physical, 1.0 }]</c>), so the split's weight total is
        /// never zero.
        /// </summary>
        public required IReadOnlyList<SkillDamagePortion> DamagePortions { get; init; }

        /// <summary>
        /// The leaf damage type the display surfaces (icon/colour) read as "the skill's type": the
        /// highest-weight portion, the first in authored order on a tie. Falls back to
        /// <see cref="EDamageType.Physical"/> for a malformed skill carrying no portions, so a display read
        /// never throws. The direct-hit pipeline reads the full <see cref="DamagePortions"/> split, not this
        /// single primary type. Resolved by the shared <see cref="PrimaryDamageTypeResolver"/> — also used by
        /// the read-contract and persisted-entity mirrors of this accessor.
        /// </summary>
        public EDamageType PrimaryDamageType =>
            PrimaryDamageTypeResolver.Resolve(DamagePortions, p => p.Weight, p => p.Type);

        public required IReadOnlyList<DamageMultiplier> DamageMultipliers { get; init; }

        public required IReadOnlyList<SkillEffect> Effects { get; init; }
    }
}
