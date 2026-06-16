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
        /// Computes the rewards for defeating <paramref name="enemy"/>. The player's power is measured from
        /// <paramref name="playerModifiers"/> — the modifier set reconstructed from the battle snapshot
        /// (<see cref="BattleSnapshot.GetModifiers"/>) — rather than the live player aggregate, so the reward
        /// is consistent with the snapshot the battle was actually simulated against.
        /// </summary>
        public DefeatRewards(IEnumerable<AttributeModifier> playerModifiers, Enemy enemy)
        {
            ExpReward = GetExpReward(playerModifiers, enemy);
        }

        private static int GetExpReward(IEnumerable<AttributeModifier> playerModifiers, Enemy enemy)
        {
            var enemyAttTotal = SumCoreAttributes(enemy.GetAttributeModifiers());
            var playerAttTotal = SumCoreAttributes(playerModifiers);
            if (playerAttTotal <= 0)
            {
                return ToIntReward(enemyAttTotal);
            }

            var attRatio = enemyAttTotal / playerAttTotal;
            // Within a ±20% band the multiplier is 1; outside it the reward scales quadratically with the
            // ratio, clamped at MaxExpRewardMultiplier so an enemy far above the player can't mint an
            // unbounded payout.
            var expMulti = attRatio is < 0.8 or > 1.2 ? Math.Pow(attRatio, 2) : 1.0;
            expMulti = Math.Min(expMulti, ServerGameConstants.MaxExpRewardMultiplier);
            return ToIntReward(enemyAttTotal * expMulti);
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
        /// Sums the additive amounts of the core attributes in <paramref name="modifiers"/>. Both
        /// combatants' power is measured the same way so the difficulty ratio compares like with like:
        /// derived attributes (e.g. MaxHealth) are excluded because they are computed from the core
        /// attributes and never appear in a player's <see cref="Player.GetAllModifiers"/>, and
        /// multiplicative modifiers are excluded because their amount is a scaling factor, not a flat
        /// point total that can be meaningfully summed.
        /// </summary>
        private static double SumCoreAttributes(IEnumerable<AttributeModifier> modifiers)
        {
            return modifiers
                .Where(mod => mod.Type == EModifierType.Additive && Attribute.IsCore(mod.Attribute))
                .Sum(mod => mod.Amount);
        }
    }
}
