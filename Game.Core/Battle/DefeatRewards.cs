using Game.Core.Enemies;
using Game.Core.Items;
using Game.Core.Players;

namespace Game.Core.Battle
{
    /// <summary>
    /// Represents the rewards that a player receives after defeating an enemy.
    /// </summary>
    public class DefeatRewards
    {
        /// <summary>
        /// The amount of experience points that the player will receive.
        /// </summary>
        public int ExpReward { get; set; }

        /// <summary>
        /// The items that the player will receive as drops.
        /// </summary>
        public IEnumerable<Item> Drops { get; set; }

        /// <summary>
        /// Creates a new instance of <see cref="DefeatRewards"/>.
        /// </summary>
        /// <param name="player"></param>
        /// <param name="enemy"></param>
        public DefeatRewards(Player player, Enemy enemy)
        {
            ExpReward = GetExpReward(player, enemy);
            Drops = enemy.RollDrops();
        }

        /// <summary>
        /// Calculates the amount of experience points that the player will receive.
        /// </summary>
        /// <param name="player"></param>
        /// <param name="enemy"></param>
        /// <returns></returns>
        private int GetExpReward(Player player, Enemy enemy)
        {
            var enemyAttTotal = enemy.GetAttributeModifiers().Sum(att => att.Amount);
            var playerAttTotal = player.StatPoints.StatPointsGained;
            var attRatio = (double)(enemyAttTotal / playerAttTotal);
            var expMulti = attRatio is < 0.8 or > 1.2 ? Math.Pow(attRatio, 2) : 1.0;
            return (int)Math.Floor((double)enemyAttTotal * expMulti);
        }
    }
}
