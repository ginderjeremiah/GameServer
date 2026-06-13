using Game.Core.Attributes.Modifiers;
using Game.Core.Enemies;
using Game.Core.Players;

namespace Game.Core.Battle
{
    /// <summary>
    /// Represents the rewards that a player receives after defeating an enemy.
    /// </summary>
    public class DefeatRewards
    {
        public int ExpReward { get; set; }

        public DefeatRewards(Player player, Enemy enemy)
        {
            ExpReward = GetExpReward(player, enemy);
        }

        private static int GetExpReward(Player player, Enemy enemy)
        {
            var enemyAttTotal = SumCoreAttributes(enemy.GetAttributeModifiers());
            var playerAttTotal = SumCoreAttributes(player.GetAllModifiers());
            if (playerAttTotal <= 0)
            {
                return (int)Math.Floor(enemyAttTotal);
            }

            var attRatio = enemyAttTotal / playerAttTotal;
            var expMulti = attRatio is < 0.8 or > 1.2 ? Math.Pow(attRatio, 2) : 1.0;
            return (int)Math.Floor(enemyAttTotal * expMulti);
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
