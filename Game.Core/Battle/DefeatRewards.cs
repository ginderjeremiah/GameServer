using Game.Core.Attributes.Modifiers;
using Game.Core.Enemies;

namespace Game.Core.Battle
{
    /// <summary>
    /// Represents the rewards that a player receives after defeating an enemy.
    /// </summary>
    public class DefeatRewards
    {
        public int ExpReward { get; set; }

        /// <summary>
        /// The difficulty curve factor for this battle — the <c>ratio²</c> band/clamp the exp reward scales by.
        /// 1 when the enemy is within ±20% of the player's power, quadratically smaller for a trivial enemy
        /// (anti-grind) and quadratically larger (clamped at
        /// <see cref="ServerGameConstants.MaxExpRewardMultiplier"/>) for an over-level one. Drives the exp
        /// reward only; the effect-based proficiency accrual no longer reads it (power-normalization subsumes
        /// it — see <see cref="PlayerPower"/>).
        /// </summary>
        public double DifficultyMultiplier { get; }

        /// <summary>
        /// The player's measured power for this battle — the sum of their core additive attribute modifiers
        /// (the same measure the difficulty ratio normalizes by). The effect-based proficiency accrual
        /// normalizes each path's activity by it (spike #1318): <c>XP = pie × clamp(activity ÷ power)</c>, so
        /// power-normalization is the continuous difficulty curve that subsumes <see cref="DifficultyMultiplier"/>.
        /// </summary>
        public double PlayerPower { get; }

        /// <summary>
        /// Computes the rewards for defeating <paramref name="enemy"/>. The player's power is measured from
        /// <paramref name="playerModifiers"/> — the modifier set reconstructed from the battle snapshot
        /// (<see cref="BattleSnapshot.GetModifiers"/>) — rather than the live player aggregate, so the reward
        /// is consistent with the snapshot the battle was actually simulated against.
        /// </summary>
        public DefeatRewards(IEnumerable<AttributeModifier> playerModifiers, Enemy enemy)
        {
            var enemyAttTotal = SumCoreAttributes(enemy.GetAttributeModifiers());
            var playerAttTotal = SumCoreAttributes(playerModifiers);
            PlayerPower = playerAttTotal;
            DifficultyMultiplier = GetDifficultyMultiplier(enemyAttTotal, playerAttTotal);
            ExpReward = ToIntReward(enemyAttTotal * DifficultyMultiplier);
        }

        private static double GetDifficultyMultiplier(double enemyAttTotal, double playerAttTotal)
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
        /// Floors <paramref name="reward"/> and clamps it into <see cref="int"/> range. The
        /// <see cref="ServerGameConstants.MaxExpRewardMultiplier"/> cap bounds the multiplier but not
        /// <c>enemyAttTotal</c> itself (a sum over core attributes scaled by author-controlled enemy level
        /// and per-level slopes), so a large authored enemy can push the product past <see cref="int.MaxValue"/>.
        /// Clamping before the cast avoids the unchecked overflow that would wrap to a negative reward.
        /// </summary>
        private static int ToIntReward(double reward)
        {
            return (int)Math.Floor(Math.Min(reward, int.MaxValue));
        }

        /// <summary>
        /// Sums the additive amounts of the core attributes in <paramref name="modifiers"/> — the "old"
        /// power measure this class is named for, and the one the combat-rating calibration report (#1533)
        /// compares the new <see cref="CombatRating"/> against. Both combatants' power is measured the same
        /// way so the difficulty ratio compares like with like: derived attributes (e.g. MaxHealth) are
        /// excluded because they are computed from the core attributes and never appear as their own
        /// modifier, and multiplicative modifiers are excluded because their amount is a scaling factor, not
        /// a flat point total that can be meaningfully summed.
        /// </summary>
        public static double SumCoreAttributes(IEnumerable<AttributeModifier> modifiers)
        {
            return modifiers
                .Where(mod => mod.Type == EModifierType.Additive && Attribute.IsCore(mod.Attribute))
                .Sum(mod => mod.Amount);
        }
    }
}
