namespace Game.Core.Players
{
    /// <summary>
    /// The experience clamp/level-up threshold loop shared by <see cref="Player.ApplyExp"/> and the offline
    /// simulator's in-loop level tracking (#1601), so the curve and the per-grant clamp cannot drift between
    /// the live and offline paths. Pure math over <see cref="GameConstants.ExpPerLevel"/> and
    /// <see cref="ServerGameConstants.MaxExpPerGrant"/> — no events, no aggregate mutation; each caller applies
    /// the result to its own state and raises whatever domain events it owns.
    /// </summary>
    public static class ExpProgression
    {
        /// <summary>One grant applied to a starting (level, exp) pair: the resulting level/exp and how many
        /// levels were gained (0 for no level-up).</summary>
        public readonly record struct Result(int Level, int Exp, int LevelsGained);

        /// <summary>
        /// Clamps <paramref name="amount"/> to <c>[0, <see cref="ServerGameConstants.MaxExpPerGrant"/>]</c>,
        /// adds it to <paramref name="exp"/>, and runs the level-up loop against
        /// <c>level * <see cref="GameConstants.ExpPerLevel"/></c> until the remaining exp is below the next
        /// threshold.
        /// </summary>
        public static Result ApplyExp(int level, int exp, int amount)
        {
            if (level < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(level), level, "Level must be at least 1.");
            }

            exp += Math.Clamp(amount, 0, ServerGameConstants.MaxExpPerGrant);
            var levelsGained = 0;
            var levelThreshold = level * GameConstants.ExpPerLevel;
            while (exp >= levelThreshold)
            {
                exp -= levelThreshold;
                level++;
                levelsGained++;
                levelThreshold = level * GameConstants.ExpPerLevel;
            }

            return new Result(level, exp, levelsGained);
        }
    }
}
