namespace Game.Core.Skills
{
    /// <summary>
    /// One weighted slice of a skill's direct-hit damage (spike #1343). A hit's single raw damage number is
    /// split across its portions by <see cref="Weight"/> — normalized at fire time (<c>raw × weight ÷ Σweights</c>),
    /// so the stored weights are raw and authoring one portion never forces rebalancing the others — and each
    /// slice runs the existing single-type pipeline (amplify → crit → resist → Toughness) under its own
    /// <see cref="Type"/>. A single-portion skill reduces byte-for-byte to the pre-feature single-typed hit.
    /// The direct-hit pipeline (<see cref="Battle.BattleContext.DamageTarget"/>) splits a hit across these
    /// portions (#1385).
    /// </summary>
    public sealed class SkillDamagePortion
    {
        /// <summary>The leaf damage type this slice deals.</summary>
        public required EDamageType Type { get; init; }

        /// <summary>The raw (un-normalized) weight of this slice within the hit. Normalized at fire time.</summary>
        public required double Weight { get; init; }
    }
}
