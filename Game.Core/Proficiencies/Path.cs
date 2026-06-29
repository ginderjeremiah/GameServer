namespace Game.Core.Proficiencies
{
    /// <summary>
    /// The XP-routing view of a path — an immutable, cached reference instance carrying the single
    /// <see cref="ActivityKey"/> the path trains on and the path's ordered tiers (each a proficiency with its
    /// cap). At battle completion the effect-based accrual sums the battle's activity for the path's key and
    /// routes it to the path's <see cref="Frontier"/> tier (the lowest un-maxed tier), claiming
    /// <c>pie × clamp(activity ÷ power)</c> for that tier (spike #1318).
    /// </summary>
    public class Path
    {
        public required int Id { get; init; }

        /// <summary>The activity this path trains on — a damage-type key, category, or combat event. A battle
        /// quantity whose key resolves to this trains the path's frontier tier (see <see cref="EActivityKey"/>).</summary>
        public required EActivityKey ActivityKey { get; init; }

        /// <summary>The path's tiers ascending by <see cref="PathTier.Ordinal"/>; empty for a degenerate path
        /// with no proficiencies (which trains nothing).</summary>
        public required IReadOnlyList<PathTier> Tiers { get; init; }

        /// <summary>
        /// The path's frontier tier for a player — the lowest-ordinal tier they have not yet maxed — or
        /// <c>null</c> when every tier is maxed (a fully-maxed path banks nothing). Because a tier opens only
        /// once the one before it is maxed (within-path order is implicit in the ordinals), the first un-maxed
        /// tier is exactly the deepest unlocked tier, so it is the tier a contribution routes to.
        /// </summary>
        /// <param name="levelOf">The player's current level in a proficiency by id (0 when never trained).</param>
        public PathTier? Frontier(Func<int, int> levelOf)
        {
            foreach (var tier in Tiers)
            {
                if (levelOf(tier.ProficiencyId) < tier.MaxLevel)
                {
                    return tier;
                }
            }

            return null;
        }

        /// <summary>
        /// The tier immediately after <paramref name="ordinal"/> within the path (its
        /// <see cref="PathTier.Ordinal"/> + 1), or <c>null</c> when <paramref name="ordinal"/> is the last
        /// tier. The within-path open trigger uses this: maxing a tier reveals and seeds the next one.
        /// </summary>
        public PathTier? NextTier(int ordinal)
        {
            foreach (var tier in Tiers)
            {
                if (tier.Ordinal == ordinal + 1)
                {
                    return tier;
                }
            }

            return null;
        }
    }

    /// <summary>One tier of a path: the proficiency at <see cref="Ordinal"/> and its level cap.</summary>
    public readonly record struct PathTier(int ProficiencyId, int Ordinal, int MaxLevel);
}
