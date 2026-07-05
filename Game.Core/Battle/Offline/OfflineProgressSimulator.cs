using Game.Core.Enemies;
using Game.Core.Players;

namespace Game.Core.Battle.Offline
{
    /// <summary>
    /// Replays a player's missed idle/boss battles over an away period and returns the accumulated results.
    /// A pure domain service: it reuses <see cref="BattleFactory"/>, <see cref="BattleSnapshot"/> and
    /// <see cref="BattleSimulator"/> and resolves catalog data through caller-supplied funcs, so it touches
    /// no persistence or application layer (applying the rewards is the orchestration sub-issue's job).
    /// <para>
    /// The away period is replayed battle-by-battle rather than sampled — the post-battle cooldown throttles
    /// the battle count, so this stays cheap. It is <b>not</b> otherwise stationary: everything that grows
    /// automatically in live play (level, and through it the class locked base and reward power) grows across
    /// the away window too (#1601), so offline play is neither better nor worse than the live idle loop it
    /// stands in for. What requires player action — stat allocations, gear, loadout — stays frozen on the
    /// captured snapshot, exactly as it would while the player is away.
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

            // A private, mutable-level copy of the snapshot carries the in-loop level growth (#1601); every
            // other captured field (gear, allocations, proficiency levels) stays frozen on the caller's own
            // snapshot instance, since those require player action. Exp is tracked alongside it so a
            // level-up lands on the same battle it would live — a player already partway to their next level
            // doesn't get an extra free battle's grace.
            var workingSnapshot = CloneForGrowth(parameters.Snapshot);
            var currentExp = parameters.StartingExp;

            // The battler that measures each victory's exp reward is re-derived whenever a level-up grows the
            // class locked base (and, through it, the signature passive) — rebuilding it eagerly here and again
            // after every level-up, rather than lazily on next use, keeps the "current rating battler" a single
            // invariant read at each DefeatRewards call below. It is never simulated (CombatRating.Rate only
            // reads it), so reusing the same instance across battles between level-ups is safe even though a
            // Battler is otherwise single-use.
            var playerBattlerForRating = BuildRatingBattler(workingSnapshot, parameters);

            // Tracks whether any battle has produced progress (a win or a loss). A run that is nothing but
            // draws is a stalemate the player can neither win nor lose; the cutoff below stops it early.
            var madeProgress = false;

            while (remainingMs > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var enemy = BuildEnemy(parameters);
                var result = SimulateBattle(workingSnapshot, parameters, enemy);

                // Rewards are earned only on a victory, measured from the current (possibly mid-window-grown)
                // rating battler like the live path. The same DefeatRewards yields both the exp and the combat
                // ratings the offline proficiency-XP accrual normalizes each path's activity by, so the two
                // payouts share one evaluation.
                var rewards = result.Victory ? new DefeatRewards(playerBattlerForRating, enemy) : null;
                if (rewards is not null)
                {
                    // Snapshot the player's rating onto this battle's stats so the offline accrual normalizes by
                    // the identical measure the live path does (spike #1526 Decision 5) — victory-only, like the
                    // rewards.
                    result.Stats.PlayerRating = rewards.PlayerRating;

                    // Apply the victory's exp in-loop, between battles — matching live, where exp lands on
                    // battle completion — via the same clamp/threshold loop Player.ApplyExp runs (#1601). A
                    // level gained here re-derives the battler at the new level for every subsequent battle, so
                    // the locked base, signature passive, and reward power measurement all grow, and the
                    // anti-grind treadmill decays offline exactly as live.
                    var progression = ExpProgression.ApplyExp(workingSnapshot.Level, currentExp, rewards.ExpReward);
                    currentExp = progression.Exp;
                    if (progression.LevelsGained > 0)
                    {
                        workingSnapshot.Level = progression.Level;
                        playerBattlerForRating = BuildRatingBattler(workingSnapshot, parameters);
                    }
                }

                outcomes.Add(new OfflineBattleOutcome(
                    enemy, result, rewards?.ExpReward ?? 0, rewards?.PlayerRating ?? 0, rewards?.EnemyRating ?? 0));

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

            return new OfflineProgressResult(
                parameters.Mode, parameters.Zone.Id, outcomes, workingSnapshot.Level, currentExp);
        }

        /// <summary>
        /// A copy of <paramref name="snapshot"/> whose <see cref="BattleSnapshot.Level"/> the simulation loop
        /// grows independently of the caller's own instance — every other field is shared by reference (never
        /// mutated by growth), since only the level changes mid-window.
        /// </summary>
        private static BattleSnapshot CloneForGrowth(BattleSnapshot snapshot) => new()
        {
            Level = snapshot.Level,
            ClassId = snapshot.ClassId,
            StatAllocations = snapshot.StatAllocations,
            EquippedItems = snapshot.EquippedItems,
            SkillIds = snapshot.SkillIds,
            ProficiencyLevels = snapshot.ProficiencyLevels,
        };

        /// <summary>
        /// Builds the battler <see cref="DefeatRewards"/> measures the player's power from — reused across
        /// battles until the next level-up requires rebuilding it.
        /// </summary>
        private static Battler BuildRatingBattler(BattleSnapshot snapshot, OfflineSimulationParameters parameters) =>
            snapshot.ToBattler(
                parameters.ResolveItem, parameters.ResolveMod, parameters.ResolveSkill,
                parameters.ResolveProficiency, parameters.ResolveClass);

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
        /// mutates battler health/effects (a battler is single-use); rebuilding the player from
        /// <paramref name="snapshot"/> — the current, possibly mid-window-grown level (#1601) — mirrors the
        /// live replay path.
        /// </summary>
        private static BattleResult SimulateBattle(BattleSnapshot snapshot, OfflineSimulationParameters parameters, Enemy enemy)
        {
            var playerBattler = snapshot.ToBattler(
                parameters.ResolveItem, parameters.ResolveMod, parameters.ResolveSkill,
                parameters.ResolveProficiency, parameters.ResolveClass);

            return new BattleSimulator(playerBattler, enemy.ToBattler(), parameters.SeedSource()).Simulate();
        }
    }
}
