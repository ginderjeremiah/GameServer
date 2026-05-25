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
            var enemyAttTotal = enemy.GetAttributeModifiers().Sum(att => att.Amount);
            var playerAttTotal = player.StatPoints.StatPointsGained;
            if (playerAttTotal == 0)
            {
                return (int)Math.Floor(enemyAttTotal);

            }

            var attRatio = enemyAttTotal / playerAttTotal;
            var expMulti = attRatio is < 0.8 or > 1.2 ? Math.Pow(attRatio, 2) : 1.0;
            return (int)Math.Floor(enemyAttTotal * expMulti);
        }
    }
}
