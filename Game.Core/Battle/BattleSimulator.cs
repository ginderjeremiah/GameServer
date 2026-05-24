using Game.Core.Enemies;
using Game.Core.Players;

namespace Game.Core.Battle
{
    public class BattleSimulator
    {
        private Battler PlayerBattler { get; set; }
        private Battler EnemyBattler { get; set; }

        private const int MsPerTick = 40;
        private const int MaxMs = MsPerTick * 10000;

        public BattleSimulator(Player player, Enemy enemy)
        {
            PlayerBattler = new Battler(player);
            EnemyBattler = new Battler(enemy);
        }

        public bool Simulate(out int totalMs)
        {
            var context = new BattleContext(PlayerBattler, EnemyBattler, MsPerTick);

            for (totalMs = MsPerTick; totalMs <= MaxMs; totalMs += MsPerTick)
            {
                PlayerBattler.Update(context);

                if (EnemyBattler.IsDead)
                {
                    return true;
                }

                context.SwapActiveAndTargetBattlers();

                EnemyBattler.Update(context);

                if (PlayerBattler.IsDead)
                {
                    return false;
                }

                context.SwapActiveAndTargetBattlers();
            }

            return false;
        }
    }
}
