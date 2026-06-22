namespace Game.Core.Proficiencies
{
    /// <summary>
    /// The absolute-falloff proficiency-XP split for a won battle (spike #982 decision 13). A constant pie,
    /// scaled by the battle's difficulty multiplier, is divided across the paths <em>represented</em> in the
    /// fight by their <strong>falloff-free attention</strong> (how much of the loadout points at each path),
    /// then each path's slice is scaled by its <strong>on-tier efficiency</strong> — the attention-weighted
    /// average falloff of its fired contributions. The un-earned remainder is <em>not</em> minted: the pie is
    /// a ceiling, not a constant, so coasting on stale skills genuinely trains slower (not just relatively).
    /// Pure and reference-data-free: the caller resolves which skills fired, the frontier tier each path
    /// routes to, and the per-skill falloff, so the math here is the testable core.
    /// </summary>
    public static class ProficiencyXpCalculator
    {
        /// <summary>
        /// One fired skill's pull on the frontier tier of a path: the tier the contribution routes to
        /// (<see cref="ProficiencyId"/>), its falloff-free <see cref="Attention"/> (<c>skillTierWeight ×
        /// contributionWeight</c>), and the absolute <see cref="Falloff"/> over the home-tier→frontier
        /// distance (<c>1</c> on-tier). Several fired skills routing to the same tier sum into that tier's
        /// attention, and their attention-weighted falloff is the tier's on-tier efficiency.
        /// </summary>
        public readonly record struct WeightedContribution(int ProficiencyId, double Attention, double Falloff);

        /// <summary>A frontier tier's share of the battle's pie (its attention slice scaled by efficiency).</summary>
        public readonly record struct ProficiencyXpSlice(int ProficiencyId, double Xp);

        /// <summary>
        /// Splits the battle's pie across the represented paths' frontier tiers. The total is
        /// <paramref name="fixedPie"/> × <paramref name="difficultyMultiplier"/>; each tier first claims a
        /// share proportional to its summed <see cref="WeightedContribution.Attention"/> (the ceiling), then
        /// that ceiling is scaled by the tier's on-tier efficiency (the attention-weighted average falloff) —
        /// the un-earned remainder evaporates rather than being redistributed, so a path's absolute pace
        /// reflects only its own staleness. Returns slices ascending by proficiency id (a stable order so the
        /// live and offline paths, and their tests, agree). Empty when nothing is represented or the total
        /// attention is non-positive.
        /// </summary>
        public static IReadOnlyList<ProficiencyXpSlice> Split(
            double fixedPie, double difficultyMultiplier, IEnumerable<WeightedContribution> contributions)
        {
            var attentionByTier = new Dictionary<int, double>();
            var earnedAttentionByTier = new Dictionary<int, double>();
            foreach (var contribution in contributions)
            {
                attentionByTier[contribution.ProficiencyId] =
                    attentionByTier.GetValueOrDefault(contribution.ProficiencyId) + contribution.Attention;
                earnedAttentionByTier[contribution.ProficiencyId] =
                    earnedAttentionByTier.GetValueOrDefault(contribution.ProficiencyId)
                        + contribution.Attention * contribution.Falloff;
            }

            var totalAttention = attentionByTier.Values.Sum();
            if (totalAttention <= 0)
            {
                return [];
            }

            var total = fixedPie * difficultyMultiplier;
            return [.. attentionByTier
                .OrderBy(pair => pair.Key)
                .Select(pair =>
                {
                    // The share of the pie this tier's attention claims (the ceiling), scaled by its on-tier
                    // efficiency — the attention-weighted average falloff. The product is total × earned /
                    // totalAttention, so dividing by the falloff-free total is what leaves the un-earned
                    // remainder unminted.
                    var ceiling = total * pair.Value / totalAttention;
                    var efficiency = earnedAttentionByTier[pair.Key] / pair.Value;
                    return new ProficiencyXpSlice(pair.Key, ceiling * efficiency);
                })];
        }
    }
}
