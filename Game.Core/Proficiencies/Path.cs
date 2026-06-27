namespace Game.Core.Proficiencies
{
    /// <summary>
    /// The XP-routing view of a path — an immutable, cached reference instance carrying the per-tier
    /// contribution falloff and the path's ordered tiers (each a proficiency with its cap). At battle
    /// completion the accrual routes a represented path to its <see cref="Frontier"/> tier (the lowest
    /// un-maxed tier) and discounts an off-tier skill's pull by <see cref="FalloffAt"/> over the tier
    /// distance, so coasting on stale skills trains the path slower in absolute terms (spike #982 decision 13).
    /// </summary>
    public class Path
    {
        public required int Id { get; init; }

        /// <summary>The geometric base of the per-tier contribution falloff (<c>1</c> = no falloff).</summary>
        public required double FalloffBase { get; init; }

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
        /// The absolute falloff multiplier for a skill homed <paramref name="distance"/> tiers below the
        /// frontier: <c>FalloffBase^distance</c>, so <c>falloff(0) = 1</c> (on-tier, full pull) and a deeper
        /// frontier discounts an older skill geometrically. Distance is always ≥ 0 — a skill never trains a
        /// tier below where it was acquired.
        /// </summary>
        public double FalloffAt(int distance) => Math.Pow(FalloffBase, distance);

        /// <summary>
        /// The tier immediately after <paramref name="ordinal"/> within the path (its
        /// <see cref="PathTier.Ordinal"/> + 1), or <c>null</c> when <paramref name="ordinal"/> is the last
        /// tier. The within-path open trigger uses this: maxing a tier reveals the next one.
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
