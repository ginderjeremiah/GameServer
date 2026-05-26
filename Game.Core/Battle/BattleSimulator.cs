namespace Game.Core.Battle
{
    public class BattleSimulator
    {
        private Battler PlayerBattler { get; set; }
        private Battler EnemyBattler { get; set; }

        private const int MsPerTick = 40;
        private const int DefaultMaxMs = MsPerTick * 10000;

        public BattleSimulator(Battler playerBattler, Battler enemyBattler)
        {
            PlayerBattler = playerBattler;
            EnemyBattler = enemyBattler;
        }

        public BattleResult Simulate(int? maxMs = null)
        {
            var limit = maxMs ?? DefaultMaxMs;
            var context = new BattleContext(PlayerBattler, EnemyBattler, MsPerTick);

            int totalMs;
            for (totalMs = MsPerTick; totalMs <= limit; totalMs += MsPerTick)
            {
                PlayerBattler.Update(context);

                if (EnemyBattler.IsDead)
                {
                    return new BattleResult(true, false, totalMs, context.Stats);
                }

                context.SwapActiveAndTargetBattlers();

                EnemyBattler.Update(context);

                if (PlayerBattler.IsDead)
                {
                    return new BattleResult(false, true, totalMs, context.Stats);
                }

                context.SwapActiveAndTargetBattlers();
            }

            return new BattleResult(false, false, totalMs - MsPerTick, context.Stats);
        }
    }
}
