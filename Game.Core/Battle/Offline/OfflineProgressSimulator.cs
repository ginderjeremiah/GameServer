using Game.Core.Enemies;

namespace Game.Core.Battle.Offline
{
    /// <summary>
    /// Replays a player's missed idle/boss battles over an away period and returns the accumulated results.
    /// A pure domain service: it reuses <see cref="BattleFactory"/>, <see cref="BattleSnapshot"/> and
    /// <see cref="BattleSimulator"/> and resolves catalog data through caller-supplied funcs, so it touches
    /// no persistence or application layer (applying the rewards is the orchestration sub-issue's job).
    /// <para>
    /// Full simulation is exact and cheap because offline battles are <em>stationary</em> (spike #879): a
    /// player's combat power and reward distribution never change while away, and the post-battle cooldown
    /// throttles the battle count, so the whole away period is replayed battle-by-battle rather than sampled.
    /// </para>
    /// </summary>
    public class OfflineProgressSimulator(BattleFactory battleFactory)
    {
        private readonly BattleFactory _battleFactory = battleFactory;

        /// <summary>
        /// Loops the mode's battle type for the whole away budget — building a fresh enemy and a fresh battle
        /// each iteration, consuming <c>battle duration + cooldown</c> from the budget per battle — until the
        /// budget (clamped to the cap) is exhausted. Wins, losses, and draws all continue to the next battle.
        /// Observes <paramref name="cancellationToken"/> so a long run unwinds promptly rather than risking the
        /// command timeout.
        /// </summary>
        public OfflineProgressResult Simulate(OfflineSimulationParameters parameters, CancellationToken cancellationToken = default)
        {
            var outcomes = new List<OfflineBattleOutcome>();

            // Clamp the away budget to the cap (away > cap clamps to cap). A non-positive budget produces an
            // empty result without simulating anything — the whole away period is skipped.
            var remainingMs = Math.Min(parameters.AwayBudgetMs, parameters.CapMs);

            // The player's rating is stationary offline (the snapshot never changes across the away window), so
            // it is computed once here and reused for every battle's DefeatRewards — cheap to do once, wasteful
            // to redo every battle (spike #1526 Decision 6). Only the enemy rating varies per encounter.
            var playerBattler = parameters.Snapshot.ToBattler(
                parameters.ResolveItem, parameters.ResolveMod, parameters.ResolveSkill,
                parameters.ResolveProficiency, parameters.ResolveClass);
            var playerRating = CombatRating.Rate(playerBattler, isPlayer: true);

            // Tracks whether any battle has produced progress (a win or a loss). A run that is nothing but
            // draws is a stalemate the player can neither win nor lose; the cutoff below stops it early.
            var madeProgress = false;

            while (remainingMs > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var enemy = BuildEnemy(parameters);
                var result = SimulateBattle(parameters, enemy);

                // Rewards are earned only on a victory. The enemy is rated fresh each encounter (closed-form,
                // cheap) from its fielded loadout — a fresh, unmutated Battler, not the one SimulateBattle just
                // mutated fighting it (spike #1526 Decision 6). The same DefeatRewards yields both the exp and
                // the player rating the offline proficiency-XP accrual normalizes each path's activity by, so
                // the two payouts share one evaluation.
                var rewards = result.Victory
                    ? new DefeatRewards(playerRating, CombatRating.Rate(enemy.ToBattler(), isPlayer: false))
                    : null;
                if (rewards is not null)
                {
                    // Snapshot the player's rating onto this battle's stats so the offline accrual
                    // max-normalizes by the identical measure the live path does (spike #1526 Decision 5) —
                    // victory-only, like the rewards.
                    result.Stats.PlayerRating = rewards.PlayerRating;
                }

                outcomes.Add(new OfflineBattleOutcome(
                    enemy, result, rewards?.ExpReward ?? 0, rewards?.PlayerRating ?? 0));

                madeProgress |= result.Victory || result.PlayerDied;

                // Each battle consumes its own duration plus the post-battle cooldown gap before the next.
                // Battle duration is always at least one tick, so the budget strictly decreases and the loop
                // terminates even with a zero cooldown.
                remainingMs -= result.TotalMs + parameters.CooldownMs;

                // CPU-waste guard: stop once the opening batch has been nothing but draws (no win, no loss) —
                // a stalemate that would otherwise burn the whole budget on maximum-duration draws for no
                // reward. Any progress in that batch disables the guard for the rest of the run.
                if (parameters.StalemateCutoffBattles is int cutoff && !madeProgress && outcomes.Count >= cutoff)
                {
                    break;
                }
            }

            return new OfflineProgressResult(parameters.Mode, parameters.Zone.Id, outcomes);
        }

        /// <summary>
        /// Builds the enemy for the next battle according to the loop mode: a random idle encounter in the
        /// zone, or the zone's deterministic dedicated boss. Mirrors the live battle-start factory calls so
        /// the enemy is built identically to an online battle.
        /// </summary>
        private Enemy BuildEnemy(OfflineSimulationParameters parameters)
        {
            return parameters.Mode switch
            {
                OfflineLoopMode.Idle => _battleFactory.CreateBattleEnemy(parameters.Zone, parameters.ResolveEnemy),
                OfflineLoopMode.Boss => _battleFactory.CreateBossEnemy(parameters.Zone, parameters.ResolveEnemy),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(parameters), parameters.Mode, $"Unhandled offline loop mode {parameters.Mode}."),
            };
        }

        /// <summary>
        /// Runs one battle with a fresh seed. Both battlers are rebuilt per battle because the simulation
        /// mutates battler health/effects (a battler is single-use); rebuilding the player from the stationary
        /// snapshot mirrors the live replay path.
        /// </summary>
        private static BattleResult SimulateBattle(OfflineSimulationParameters parameters, Enemy enemy)
        {
            var playerBattler = parameters.Snapshot.ToBattler(
                parameters.ResolveItem, parameters.ResolveMod, parameters.ResolveSkill,
                parameters.ResolveProficiency, parameters.ResolveClass);
            var enemyBattler = enemy.ToBattler();

            return new BattleSimulator(playerBattler, enemyBattler, parameters.SeedSource()).Simulate();
        }
    }
}
