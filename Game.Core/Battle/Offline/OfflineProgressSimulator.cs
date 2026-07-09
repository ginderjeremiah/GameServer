using Game.Core.Enemies;
using Game.Core.Players;
using Game.Core.Proficiencies;

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
    /// automatically in live play (level and through it the class locked base and reward power (#1601), and
    /// proficiency levels and their attribute payouts (#1602)) grows across the away window too, so offline
    /// play is neither better nor worse than the live idle loop it stands in for. What requires player action —
    /// stat allocations, gear, loadout — stays frozen on the captured snapshot, exactly as it would while the
    /// player is away.
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
        /// enemy/seed just simulated, with its true elapsed-so-far offset, and the snapshot — possibly grown by
        /// prior credited victories, #1758 — it was actually simulated against) for the orchestration to hand
        /// back as an already-active battle, and the loop stops (there is no away time left beyond it). This keeps
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

            // A private, mutable-level copy of the snapshot carries the in-loop level growth (#1601) and the
            // in-loop proficiency growth (#1602); every other captured field (gear, allocations) stays frozen
            // on the caller's own snapshot instance, since those require player action. Exp is tracked
            // alongside it so a level-up lands on the same battle it would live — a player already partway to
            // their next level doesn't get an extra free battle's grace.
            var workingSnapshot = CloneForGrowth(parameters.Snapshot);
            var currentExp = parameters.StartingExp;

            // The in-loop proficiency progress store (#1602): keyed by the same mutable ProficiencyLevelSnapshot
            // instances workingSnapshot.ProficiencyLevels holds, so growing a proficiency in place is
            // immediately visible to the next battle's ToBattler() call with no separate write-back step.
            var proficiencyById = workingSnapshot.ProficiencyLevels.ToDictionary(p => p.ProficiencyId);
            var catalog = new ProficiencyCatalog(
                parameters.ResolveProficiency, parameters.ResolvePath, parameters.PathsForActivityKey, parameters.DependentsOf);

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

            OfflinePendingBattle? pendingBattle = null;

            while (remainingMs > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var enemy = BuildEnemy(parameters);
                var seed = parameters.SeedSource();
                var result = SimulateBattle(workingSnapshot, parameters, enemy, seed);

                if (result.TotalMs > remainingMs)
                {
                    // The battle's own duration doesn't fit the real budget still remaining — the away window
                    // ended mid-fight. Not a completed outcome: carry it forward uncredited, at the real time
                    // that had elapsed into it (exactly the budget remaining when this attempt started), and
                    // stop — there is no away time left beyond this unconcluded fight.
                    pendingBattle = new OfflinePendingBattle(enemy, seed, (int)remainingMs, workingSnapshot);
                    break;
                }

                // Rewards are earned only on a victory, measured from the current (possibly mid-window-grown)
                // rating battler like the live path. The same DefeatRewards yields both the exp and the combat
                // ratings the offline proficiency-XP accrual normalizes each path's activity by, so the two
                // payouts share one evaluation.
                var rewards = result.Victory ? new DefeatRewards(playerBattlerForRating, enemy) : null;
                var proficiencyGains = ProficiencyAccrualResult.Empty;
                if (rewards is not null)
                {
                    // Snapshot the player's rating onto this battle's stats so the offline accrual normalizes by
                    // the identical measure the live path does (spike #1526 Decision 5) — victory-only, like the
                    // rewards.
                    result.Stats.PlayerRating = rewards.PlayerRating;

                    // Accrue proficiency XP in-loop (#1602): the same domain math the live per-battle path
                    // runs, against this run's own working proficiency state, so a milestone attribute payout
                    // crossed here is already baked into the next battle's snapshot below — unlike the pre-#1602
                    // behavior, where the whole window fought with window-start proficiency bonuses because the
                    // accrual only ran after the simulation concluded.
                    var proficiencyLeveledUp = false;
                    proficiencyGains = ProficiencyAccrual.Accrue(
                        catalog, result.Stats, Math.Max(rewards.PlayerRating, rewards.EnemyRating),
                        id => proficiencyById.TryGetValue(id, out var p) ? p.Level : 0,
                        id => proficiencyById.TryGetValue(id, out var p) ? p.Xp : 0m,
                        (id, level, xp) =>
                        {
                            if (proficiencyById.TryGetValue(id, out var existing))
                            {
                                proficiencyLeveledUp |= existing.Level != level;
                                existing.Level = level;
                                existing.Xp = xp;
                            }
                            else
                            {
                                // A never-before-trained proficiency (no captured entry): its implicit starting
                                // level is 0 (the levelOf default above), so reaching any level here — even on
                                // its first-ever accrual — is a level-up and must trigger a rating rebuild.
                                proficiencyLeveledUp |= level != 0;
                                var created = new ProficiencyLevelSnapshot { ProficiencyId = id, Level = level, Xp = xp };
                                proficiencyById[id] = created;
                                workingSnapshot.ProficiencyLevels.Add(created);
                            }
                        });

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
                    }

                    // Re-derive the rating battler whenever either growth vector actually moved an attribute
                    // modifier: a player level-up (the locked base) or a proficiency level-up (its per-level/
                    // milestone bonus) — never on XP alone, which changes nothing ModifiersForLevel reads. This
                    // is what makes the reward-power measurement — and through it the proficiency accrual's own
                    // rating denominator on the next battle — grow with proficiency, not just level.
                    if (progression.LevelsGained > 0 || proficiencyLeveledUp)
                    {
                        playerBattlerForRating = BuildRatingBattler(workingSnapshot, parameters);
                    }
                }

                outcomes.Add(new OfflineBattleOutcome(
                    enemy, result, rewards?.ExpReward ?? 0, rewards?.PlayerRating ?? 0, rewards?.EnemyRating ?? 0,
                    proficiencyGains));

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

            return new OfflineProgressResult(
                parameters.Mode, parameters.Zone.Id, outcomes, remainderMs, pendingBattle,
                workingSnapshot.Level, currentExp);
        }

        /// <summary>
        /// A copy of <paramref name="snapshot"/> whose <see cref="BattleSnapshot.Level"/> (#1601) and
        /// <see cref="BattleSnapshot.ProficiencyLevels"/> (#1602) the simulation loop grows independently of
        /// the caller's own instance — every other field is shared by reference (never mutated by growth).
        /// <see cref="BattleSnapshot.ProficiencyLevels"/> is deep-copied (a fresh list of fresh
        /// <see cref="ProficiencyLevelSnapshot"/> instances), not shared, since the loop mutates existing
        /// entries in place and appends newly-trained ones — sharing the caller's list/instances would leak
        /// in-loop growth back onto the frozen snapshot the caller (and any other reader) still holds.
        /// </summary>
        private static BattleSnapshot CloneForGrowth(BattleSnapshot snapshot) => new()
        {
            Level = snapshot.Level,
            ClassId = snapshot.ClassId,
            StatAllocations = snapshot.StatAllocations,
            EquippedItems = snapshot.EquippedItems,
            SkillIds = snapshot.SkillIds,
            ProficiencyLevels = snapshot.ProficiencyLevels
                .Select(p => new ProficiencyLevelSnapshot { ProficiencyId = p.ProficiencyId, Level = p.Level, Xp = p.Xp })
                .ToList(),
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
        /// Runs one battle with the given seed (the caller draws it up front so it can carry the same seed
        /// forward on a pending-battle hand-back, #1596), rebuilding the player from
        /// <paramref name="snapshot"/> — the current, possibly mid-window-grown level (#1601) — which mirrors
        /// the live replay path. Both battlers are rebuilt per battle because the simulation mutates battler
        /// health/effects (a battler is single-use).
        /// </summary>
        private static BattleResult SimulateBattle(BattleSnapshot snapshot, OfflineSimulationParameters parameters, Enemy enemy, uint seed)
        {
            var playerBattler = snapshot.ToBattler(
                parameters.ResolveItem, parameters.ResolveMod, parameters.ResolveSkill,
                parameters.ResolveProficiency, parameters.ResolveClass);

            return new BattleSimulator(playerBattler, enemy.ToBattler(), seed).Simulate();
        }
    }
}
