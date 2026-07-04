namespace Game.Core.Proficiencies
{
    /// <summary>
    /// The effect-based proficiency-XP claim for a won battle (spike #1318, max-normalized per spike #1526
    /// Decision 5). Each path independently claims <c>pie × activity ÷ ratingDenominator</c> of XP, routed to its
    /// frontier tier — the claims overlap and need <em>not</em> sum to 1, because there is no shared pie to split
    /// (different paths and axes train in parallel). <paramref name="ratingDenominator"/> is
    /// <c>max(playerRating, enemyRating)</c>: above your weight the claim rate is what a matched player would
    /// earn, at or below it the treadmill is unchanged (spike #1526 Decision 5) — so max-normalization
    /// <em>subsumes</em> the enemy-bounty reward curve's own difficulty shaping without applying it twice.
    /// <para>
    /// There is deliberately no clamp on the ratio (unlike the pre-#1532 <c>MaxExpRewardMultiplier</c>-mirroring
    /// clamp): a path's per-battle activity is itself bounded by the enemy's health pool (a killing blow's
    /// overkill trains nothing, #1482), so <c>activity ≤ enemyHP</c> already bounds the claim without a separate
    /// safety cap.
    /// </para>
    /// Pure and reference-data-free: the caller resolves each path's frontier tier and summed activity, so the
    /// math here is the testable core.
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
        /// <c>activity ÷ ratingDenominator</c>, independent of the others (no shared pie — the claims overlap and
        /// need not sum to 1). Returns slices ascending by proficiency id (a stable order so the live and offline
        /// paths, and their tests, agree). A path with non-positive activity earns nothing and is omitted; a
        /// non-positive <paramref name="ratingDenominator"/> — a degenerate state, since a real battler's rating
        /// is always strictly positive (<see cref="Battle.CombatRating.Rate"/>'s own floor) — yields no slices
        /// rather than dividing by zero.
        /// </summary>
        public static IReadOnlyList<ProficiencyXpSlice> Split(
            double pie, double ratingDenominator, IEnumerable<PathActivity> activities)
        {
            if (ratingDenominator <= 0)
            {
                return [];
            }

            return [.. activities
                .Where(activity => activity.Activity > 0)
                .OrderBy(activity => activity.ProficiencyId)
                .Select(activity => new ProficiencyXpSlice(
                    activity.ProficiencyId, pie * activity.Activity / ratingDenominator))];
        }
    }
}
