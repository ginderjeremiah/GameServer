namespace Game.Core.Battle
{
    public class BattleSimulator
    {
        private Battler PlayerBattler { get; set; }
        private Battler EnemyBattler { get; set; }
        private readonly uint _seed;

        public BattleSimulator(Battler playerBattler, Battler enemyBattler, uint seed)
        {
            PlayerBattler = playerBattler;
            EnemyBattler = enemyBattler;
            _seed = seed;
        }

        public BattleResult Simulate(int? maxMs = null)
        {
            var msPerTick = GameConstants.MsPerTick;
            var limit = maxMs ?? GameConstants.DefaultMaxBattleMs;
            // One Mulberry32 seeded once from the battle seed and advanced in lockstep with the frontend, so
            // both simulators draw the crit/dodge/block rolls from the identical stream (battle parity).
            var context = new BattleContext(PlayerBattler, EnemyBattler, msPerTick, new Mulberry32(_seed));

            int totalMs;
            for (totalMs = msPerTick; totalMs <= limit; totalMs += msPerTick)
            {
                // Expire timed effects at the start of the tick, before either side fires, so an effect
                // influences exactly DurationMs / tickSize ticks (counting its application tick).
                PlayerBattler.AdvanceEffects(msPerTick);
                EnemyBattler.AdvanceEffects(msPerTick);

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

                // End-of-tick damage/heal-over-time, reached only with both battlers still alive (matching
                // the skill-exchange early returns). The enemy resolves first, so an enemy DoT kill awards
                // victory before the player's DoT applies — a same-tick mutual DoT kill favours the player.
                context.ResolveDamageOverTime();

                if (EnemyBattler.IsDead)
                {
                    return new BattleResult(true, false, totalMs, context.Stats);
                }

                if (PlayerBattler.IsDead)
                {
                    return new BattleResult(false, true, totalMs, context.Stats);
                }
            }

            return new BattleResult(false, false, totalMs - msPerTick, context.Stats);
        }
    }
}
