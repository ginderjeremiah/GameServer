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
        /// each iteration — until the budget (clamped to the cap) is exhausted. Wins, losses, and draws all
        /// continue to the next battle. Observes <paramref name="cancellationToken"/> so a long run unwinds
        /// promptly rather than risking the command timeout.
        /// <para>
        /// Each candidate battle is checked against the <em>remaining</em> budget before being credited
        /// (#1596): if its own duration fits, it is a real completed outcome — credited normally, consuming
        /// <c>duration + cooldown</c> from the budget. If it doesn't fit, the away-window boundary falls
        /// inside it rather than after it: it did not really conclude, so it is not credited as a win/loss/
        /// draw at all — it is carried forward as <see cref="OfflineProgressResult.PendingBattle"/> (the exact
        /// enemy/seed just simulated, with its true elapsed-so-far offset) for the orchestration to hand back
        /// as an already-active battle, and the loop stops (there is no away time left beyond it). This keeps
        /// the loop from ever double-counting the battle straddling the boundary as both a completed win and
        /// a resumed fight.
        /// </para>
        /// </summary>
        public OfflineProgressResult Simulate(OfflineSimulationParameters parameters, CancellationToken cancellationToken = default)
        {
            var outcomes = new List<OfflineBattleOutcome>();

            // Clamp the away budget to the cap (away > cap clamps to cap). A non-positive budget produces an
            // empty result without simulating anything — the whole away period is skipped.
            var remainingMs = Math.Min(parameters.AwayBudgetMs, parameters.CapMs);

            // The player's rating is stationary offline (their combat power never changes while away), so the
            // battler that measures each victory's exp reward never changes — build it once and reuse it for
            // every battle's DefeatRewards. It is never simulated (CombatRating.Rate only reads it), so reusing
            // the same instance across battles is safe even though a Battler is otherwise single-use.
            var playerBattlerForRating = parameters.Snapshot.ToBattler(
                parameters.ResolveItem, parameters.ResolveMod, parameters.ResolveSkill,
                parameters.ResolveProficiency, parameters.ResolveClass);

            // Tracks whether any battle has produced progress (a win or a loss). A run that is nothing but
            // draws is a stalemate the player can neither win nor lose; the cutoff below stops it early.
            var madeProgress = false;

            OfflinePendingBattle? pendingBattle = null;

            while (remainingMs > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var enemy = BuildEnemy(parameters);
                var seed = parameters.SeedSource();
                var result = SimulateBattle(parameters, enemy, seed);

                if (result.TotalMs > remainingMs)
                {
                    // The battle's own duration doesn't fit the real budget still remaining — the away window
                    // ended mid-fight. Not a completed outcome: carry it forward uncredited, at the real time
                    // that had elapsed into it (exactly the budget remaining when this attempt started), and
                    // stop — there is no away time left beyond this unconcluded fight.
                    pendingBattle = new OfflinePendingBattle(enemy, seed, (int)remainingMs);
                    break;
                }

                // Rewards are earned only on a victory, measured from the stationary snapshot like the live
                // path. The same DefeatRewards yields both the exp and the combat ratings the offline
                // proficiency-XP accrual normalizes each path's activity by, so the two payouts share one
                // evaluation.
                var rewards = result.Victory ? new DefeatRewards(playerBattlerForRating, enemy) : null;
                if (rewards is not null)
                {
                    // Snapshot the player's rating onto this battle's stats so the offline accrual normalizes by
                    // the identical measure the live path does (spike #1526 Decision 5) — victory-only, like the
                    // rewards.
                    result.Stats.PlayerRating = rewards.PlayerRating;
                }

                outcomes.Add(new OfflineBattleOutcome(
                    enemy, result, rewards?.ExpReward ?? 0, rewards?.PlayerRating ?? 0, rewards?.EnemyRating ?? 0));

                madeProgress |= result.Victory || result.PlayerDied;

                // The battle fit, so it is a real completed outcome: consume its duration plus the post-battle
                // cooldown gap before the next. Battle duration is always at least one tick, so the budget
                // strictly decreases and the loop terminates even with a zero cooldown.
                remainingMs -= result.TotalMs + parameters.CooldownMs;

                // CPU-waste guard: stop once the opening batch has been nothing but draws (no win, no loss) —
                // a stalemate that would otherwise burn the whole budget on maximum-duration draws for no
                // reward. Any progress in that batch disables the guard for the rest of the run.
                if (parameters.StalemateCutoffBattles is int cutoff && !madeProgress && outcomes.Count >= cutoff)
                {
                    break;
                }
            }

            // The residual cooldown (#1596): once every credited battle's own duration has fit the budget it
            // was drawn against, the loop can still exhaust the budget mid-cooldown — remainingMs lands at or
            // below zero, and its magnitude is that overshoot, always at most CooldownMs (a battle whose own
            // duration didn't fit is never credited; it becomes pendingBattle above instead). Not applicable
            // when nothing was simulated, when a pending battle was carried instead, or when the stalemate
            // cutoff stopped the loop early with real budget still unspent (remainingMs positive for a
            // different reason — a CPU-waste guard, not an overshoot).
            var remainderMs = pendingBattle is null && outcomes.Count > 0 ? Math.Max(0, -remainingMs) : 0;

            return new OfflineProgressResult(parameters.Mode, parameters.Zone.Id, outcomes, remainderMs, pendingBattle);
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
        /// Runs one battle with the given seed (the caller draws it up front so it can carry the same seed
        /// forward on a pending-battle hand-back, #1596). Both battlers are rebuilt per battle because the
        /// simulation mutates battler health/effects (a battler is single-use); rebuilding the player from the
        /// stationary snapshot mirrors the live replay path.
        /// </summary>
        private static BattleResult SimulateBattle(OfflineSimulationParameters parameters, Enemy enemy, uint seed)
        {
            var playerBattler = parameters.Snapshot.ToBattler(
                parameters.ResolveItem, parameters.ResolveMod, parameters.ResolveSkill,
                parameters.ResolveProficiency, parameters.ResolveClass);

            return new BattleSimulator(playerBattler, enemy.ToBattler(), seed).Simulate();
        }
    }
}
