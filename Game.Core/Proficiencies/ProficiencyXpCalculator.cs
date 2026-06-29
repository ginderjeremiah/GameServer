namespace Game.Core.Proficiencies
{
    /// <summary>
    /// The effect-based proficiency-XP claim for a won battle (spike #1318). Each path independently claims
    /// <c>pie × clamp(activity ÷ power)</c> of XP, routed to its frontier tier — the claims overlap and need
    /// <em>not</em> sum to 1, because there is no shared pie to split (different paths and axes train in
    /// parallel). <c>activity ÷ power</c> is the continuous difficulty ratio (a quantity relative to the
    /// player's total attributes), so power-normalization <em>subsumes</em> the banded difficulty multiplier:
    /// it is deliberately not applied again, which would put power in the denominator twice and open a
    /// strip-power exploit. The clamp mirrors <see cref="ServerGameConstants.MaxExpRewardMultiplier"/>. Pure and
    /// reference-data-free: the caller resolves each path's frontier tier and summed activity, so the math here
    /// is the testable core.
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
        /// <c>min(activity ÷ power, </c><paramref name="maxMultiplier"/><c>)</c>, independent of the others
        /// (no shared pie — the claims overlap and need not sum to 1). Returns slices ascending by proficiency
        /// id (a stable order so the live and offline paths, and their tests, agree). A path with non-positive
        /// activity earns nothing and is omitted; a non-positive <paramref name="power"/> — a degenerate state,
        /// since a real character always carries positive locked-base attributes — yields no slices rather than
        /// dividing by zero.
        /// </summary>
        public static IReadOnlyList<ProficiencyXpSlice> Split(
            double pie, double power, double maxMultiplier, IEnumerable<PathActivity> activities)
        {
            if (power <= 0)
            {
                return [];
            }

            return [.. activities
                .Where(activity => activity.Activity > 0)
                .OrderBy(activity => activity.ProficiencyId)
                .Select(activity => new ProficiencyXpSlice(
                    activity.ProficiencyId, pie * Math.Min(activity.Activity / power, maxMultiplier)))];
        }
    }
}
