using Game.Core.Enemies;
using Game.Core.Inventories;
using Game.Core.Players;

namespace Game.Core.Battle
{
    public class BattleSimulator
    {
        private Mulberry32 Rng { get; set; }
        private Battler Player { get; set; }
        private Battler Enemy { get; set; }

        private const int msPerTick = 40;
        private const int maxMs = msPerTick * 10000;

        public BattleSimulator(Player player, Inventory inventory, Enemy enemy, uint seed)
        {
            Rng = new Mulberry32(seed);
            Player = new Battler(player, inventory);
            Enemy = new Battler(enemy);
        }

        public bool Simulate(out int totalMs)
        {
            var context = InitializeContext();
            for (totalMs = msPerTick; totalMs <= maxMs; totalMs += msPerTick)
            {
                Player.Update(context);

                if (Enemy.IsDead)
                {
                    return true;
                }

                context.ActiveBattler = Enemy;
                context.TargetBattler = Player;

                Enemy.Update(context);

                if (Player.IsDead)
                {
                    return false;
                }

                context.ActiveBattler = Player;
                context.TargetBattler = Enemy;
            }

            return false;
        }

        private BattleContext InitializeContext()
        {
            return new BattleContext
            {
                TimeDelta = msPerTick,
                ActiveBattler = Player,
                TargetBattler = Enemy,
            };
        }
    }
}
