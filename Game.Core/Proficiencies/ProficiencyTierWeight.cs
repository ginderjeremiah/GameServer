namespace Game.Core.Proficiencies
{
    /// <summary>
    /// Maps a contributing skill's rarity tier to its proficiency-XP weight (the <c>skillTierWeight</c> in the
    /// fixed-pie split, spike #982 decision 4 + tracker #1123). A rarer skill carries more <em>attention</em>,
    /// so it pulls a larger share of a won battle's XP pie toward the path it feeds: the deeper, rarer skills
    /// that open the advanced tiers pace those tiers faster than a build still leaning on common starter
    /// skills. It never walls the low tiers — every weight is positive, and because the split is relative the
    /// cheapest skill still earns the full on-tier pie when it fights alone (its weight cancels), so a common
    /// skill trains slower only when a rarer one is competing for the same pie.
    /// <para>
    /// The weight is geometric in the rarity rank — <c>Growth^(rank − 1)</c>, with Common (the baseline) at
    /// <c>1</c> — mirroring the geometric curves used elsewhere (the per-tier XP curve, the path falloff). The
    /// growth base is a strawman value, tunable during balancing.
    /// </para>
    /// </summary>
    public static class ProficiencyTierWeight
    {
        /// <summary>
        /// Per-rarity-rank multiplier on a skill's proficiency-XP attention (~1.5× per tier, so Mythic ≈ 7.6×
        /// Common). A strawman base — tune against the authored XP curves and the rarity distribution.
        /// </summary>
        private const double Growth = 1.5;

        /// <summary>The tier weight for <paramref name="rarity"/>: <c>Growth^(rank − 1)</c>, Common at <c>1</c>.</summary>
        public static double For(ERarity rarity) => Math.Pow(Growth, (int)rarity - (int)ERarity.Common);
    }
}
