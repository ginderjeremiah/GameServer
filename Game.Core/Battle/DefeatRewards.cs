using Game.Core.Attributes.Modifiers;

namespace Game.Core.Battle
{
    /// <summary>
    /// Represents the rewards that a player receives after defeating an enemy.
    /// </summary>
    public class DefeatRewards
    {
        public int ExpReward { get; set; }

        /// <summary>
        /// The enemy-authored bounty curve's <c>min(r, 1)²</c> factor (<c>r = enemyRating ÷ playerRating</c>,
        /// spike #1526 Decision 4): <c>1</c> at a matched fight, quadratically smaller against a weaker enemy
        /// (anti-grind), and saturating at <c>1</c> — never higher — against a stronger one, so punching up
        /// pays the bigger bounty at a faster kill rate rather than an unbounded per-kill jackpot. Drives the
        /// exp reward only; the proficiency accrual doesn't read it (max-normalization is its own difficulty
        /// curve — see <see cref="PlayerRating"/>).
        /// </summary>
        public double DifficultyMultiplier { get; }

        /// <summary>
        /// The player's <see cref="CombatRating"/> for this battle. The proficiency accrual max-normalizes each
        /// path's activity against <c>max(playerRating, enemyRating)</c> (spike #1526 Decision 5), so it is
        /// exposed here for reuse rather than recomputed.
        /// </summary>
        public double PlayerRating { get; }

        /// <summary>
        /// Computes the rewards for defeating an enemy rated at <paramref name="enemyRating"/>, for a player
        /// rated at <paramref name="playerRating"/> — both precomputed via <see cref="CombatRating.Rate"/> by
        /// the caller (the live path rates once per battle from the frozen snapshot; the offline path rates the
        /// player once per stationary away window and reuses it across every battle, recomputing only the
        /// enemy rating per encounter). <see cref="CombatRating.Rate"/> always returns a strictly-positive
        /// value, so no non-positive-rating guard is needed here (unlike the retired <c>SumCoreAttributes</c>
        /// measure, which really could be zero for an unallocated character).
        /// </summary>
        public DefeatRewards(double playerRating, double enemyRating)
        {
            PlayerRating = playerRating;
            var ratio = Math.Min(enemyRating / playerRating, 1.0);
            DifficultyMultiplier = ratio * ratio;
            ExpReward = ToIntReward(ServerGameConstants.CombatRatingXpScale * enemyRating * DifficultyMultiplier);
        }

        /// <summary>
        /// The <b>retired</b> production difficulty curve: <c>1</c> within a ±20% band of matched
        /// <c>SumCoreAttributes</c> totals, <c>ratio²</c> outside it, clamped at
        /// <see cref="ServerGameConstants.MaxExpRewardMultiplier"/>. No longer called by the constructor above
        /// — kept public only so the combat-rating calibration report (#1533) can model the old-vs-new pricing
        /// comparison against authored content.
        /// </summary>
        public static double GetDifficultyMultiplier(double enemyAttTotal, double playerAttTotal)
        {
            // No player investment yet: fall back to a neutral multiplier (the reward is then the floored
            // enemy total), matching the original guard before the curve was factored out.
            if (playerAttTotal <= 0)
            {
                return 1.0;
            }

            var attRatio = enemyAttTotal / playerAttTotal;
            // Within a ±20% band the multiplier is 1; outside it the reward scales quadratically with the
            // ratio, clamped at MaxExpRewardMultiplier so an enemy far above the player can't mint an
            // unbounded payout.
            var expMulti = attRatio is < 0.8 or > 1.2 ? Math.Pow(attRatio, 2) : 1.0;
            return Math.Min(expMulti, ServerGameConstants.MaxExpRewardMultiplier);
        }

        /// <summary>
        /// Floors <paramref name="reward"/> and clamps it into <see cref="int"/> range. The bounty curve's
        /// <c>min(r, 1)²</c> factor is itself bounded at <c>1</c>, but <c>enemyRating</c> is not (an
        /// author-controlled enemy level and per-level slopes can drive it arbitrarily high), so a large
        /// authored enemy can still push the product past <see cref="int.MaxValue"/>. Clamping before the cast
        /// avoids the unchecked overflow that would wrap to a negative reward.
        /// </summary>
        private static int ToIntReward(double reward)
        {
            return (int)Math.Floor(Math.Min(reward, int.MaxValue));
        }

        /// <summary>
        /// Sums the additive amounts of the core attributes in <paramref name="modifiers"/> — the <b>retired</b>
        /// production power measure this class is named for (spike #1526 superseded it with <see cref="CombatRating"/>),
        /// kept only so the combat-rating calibration report (#1533) can model the old curve for its old-vs-new
        /// comparison columns. Both combatants' power was measured the same way so the difficulty ratio compared
        /// like with like: derived attributes (e.g. MaxHealth) are excluded because they are computed from the
        /// core attributes and never appear as their own modifier, and multiplicative modifiers are excluded
        /// because their amount is a scaling factor, not a flat point total that can be meaningfully summed.
        /// </summary>
        public static double SumCoreAttributes(IEnumerable<AttributeModifier> modifiers)
        {
            return modifiers
                .Where(mod => mod.Type == EModifierType.Additive && Attribute.IsCore(mod.Attribute))
                .Sum(mod => mod.Amount);
        }
    }
}
