namespace Game.Core.Proficiencies
{
    /// <summary>
    /// The effect-based proficiency-XP claim for a won battle (spike #1318, max-normalized per spike #1526
    /// Decision 5). Each path independently claims <c>pie × activity ÷ max(playerRating, enemyRating)</c> of
    /// XP, routed to its frontier tier — the claims overlap and need <em>not</em> sum to 1, because there is no
    /// shared pie to split (different paths and axes train in parallel). Max-normalization <em>is</em> the
    /// continuous difficulty curve — above your weight the claim rate is what a matched player would earn; at
    /// or below, training is unchanged — so <see cref="Battle.DefeatRewards.DifficultyMultiplier"/> is
    /// deliberately not applied again. There is no clamp on the ratio: <c>activity</c> is itself bounded by the
    /// enemy's health pool per battle, which already bounds the claim naturally. Pure and reference-data-free:
    /// the caller resolves each path's frontier tier and summed activity, so the math here is the testable core.
    /// </summary>
    public static class ProficiencyXpCalculator
    {
        /// <summary>
        /// One path's claim input: the frontier tier the claim routes to (<see cref="ProficiencyId"/>) and the
        /// path's total <see cref="Activity"/> this battle (e.g. the summed damage of the path's activity key).
        /// </summary>
        public readonly record struct PathActivity(int ProficiencyId, double Activity);

        /// <summary>A frontier tier's earned XP for the battle.</summary>
        public readonly record struct ProficiencyXpSlice(int ProficiencyId, double Xp);

        /// <summary>
        /// The per-path XP claims for the battle: each path earns <paramref name="pie"/> ×
        /// <c>activity ÷ max(</c><paramref name="playerRating"/>, <paramref name="enemyRating"/><c>)</c>,
        /// independent of the others (no shared pie — the claims overlap and need not sum to 1). Returns slices
        /// ascending by proficiency id (a stable order so the live and offline paths, and their tests, agree). A
        /// path with non-positive activity earns nothing and is omitted; a non-positive normalizer — a
        /// degenerate state, since <see cref="Battle.CombatRating.Rate"/> always returns a strictly-positive
        /// value — yields no slices rather than dividing by zero.
        /// </summary>
        public static IReadOnlyList<ProficiencyXpSlice> Split(
            double pie, double playerRating, double enemyRating, IEnumerable<PathActivity> activities)
        {
            var normalizer = Math.Max(playerRating, enemyRating);
            if (normalizer <= 0)
            {
                return [];
            }

            return [.. activities
                .Where(activity => activity.Activity > 0)
                .OrderBy(activity => activity.ProficiencyId)
                .Select(activity => new ProficiencyXpSlice(
                    activity.ProficiencyId, pie * activity.Activity / normalizer))];
        }
    }
}
