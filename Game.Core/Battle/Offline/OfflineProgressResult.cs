namespace Game.Core.Battle.Offline
{
    /// <summary>
    /// The accumulated result of replaying a player's missed idle/boss battles over an away period. Carries
    /// the per-battle outcomes the orchestration layer needs to apply rewards and consolidate statistics,
    /// plus the run-level aggregates the welcome-back summary surfaces. The simulator only computes and
    /// returns this — it never mutates the player or persists anything (that is the orchestration sub-issue).
    /// </summary>
    public class OfflineProgressResult
    {
        /// <summary>The loop mode that was simulated, constant for the whole run.</summary>
        public OfflineLoopMode Mode { get; }

        /// <summary>The zone the loop ran in, constant for the whole run. The zone every recorded battle is
        /// attributed to.</summary>
        public int ZoneId { get; }

        /// <summary>Whether the simulated loop was boss-farming, derived from <see cref="Mode"/> — the
        /// boss-battle flag every recorded battle carries into the statistics path.</summary>
        public bool IsBossBattle => Mode == OfflineLoopMode.Boss;

        /// <summary>Every simulated battle, in order, with its result and earned exp.</summary>
        public IReadOnlyList<OfflineBattleOutcome> Battles { get; }

        /// <summary>The number of battles simulated.</summary>
        public int BattlesSimulated => Battles.Count;

        /// <summary>Battles the player won.</summary>
        public int Wins { get; }

        /// <summary>Battles the player lost (died).</summary>
        public int Losses { get; }

        /// <summary>Battles that ended in a draw (neither combatant died before the battle cap).</summary>
        public int Draws { get; }

        /// <summary>The total exp earned across all victories — equal to the sum of each victory's
        /// <see cref="OfflineBattleOutcome.ExpReward"/>.</summary>
        public long TotalExp { get; }

        /// <summary>
        /// The residual post-battle cooldown left once the away-window crediting loop exhausts the budget
        /// inside a completed battle's cooldown (#1596) — always at most the cooldown length, since a
        /// battle whose own duration doesn't fit in the remaining budget is never credited (see
        /// <see cref="PendingBattle"/> instead). 0 when nothing was simulated, when the boundary lands inside
        /// a battle rather than its cooldown (<see cref="PendingBattle"/> is set instead), or when the
        /// stalemate cutoff — a CPU-waste guard, not an overshoot — stopped the loop early with genuine
        /// unspent budget. The orchestration layer carries this forward as a residual <c>PlayerState</c>
        /// cooldown rather than dropping it when the live loop resumes.
        /// </summary>
        public long RemainderMs { get; }

        /// <summary>
        /// Non-null when the away-window boundary falls <em>inside</em> a battle rather than a completed
        /// battle's cooldown (#1596): that battle's own duration didn't fit the remaining budget, so it was
        /// never credited as a win/loss/draw — it is carried forward here (same enemy/seed, its true elapsed
        /// offset) for the orchestration to hand back as an already-active battle, mirroring the leading-edge
        /// stale-battle hand-back (#1595). Mutually exclusive with a non-zero <see cref="RemainderMs"/>.
        /// </summary>
        public OfflinePendingBattle? PendingBattle { get; }

        /// <summary>
        /// The player's level at the end of the simulated window, per the simulator's own in-loop growth
        /// accounting (#1601) — <see cref="Game.Core.Players.Player.GrantOfflineExp"/> applying the same
        /// victory rewards from the same starting level/exp must land on this exact value, since both paths
        /// run the shared <see cref="Game.Core.Players.ExpProgression.ApplyExp"/> loop. Defaults to <c>0</c>
        /// for a result built without a simulated run (e.g. a hand-built test fixture).
        /// </summary>
        public int EndingLevel { get; }

        /// <summary>The player's residual exp at the end of the simulated window, alongside
        /// <see cref="EndingLevel"/>.</summary>
        public int EndingExp { get; }

        public OfflineProgressResult(
            OfflineLoopMode mode, int zoneId, IReadOnlyList<OfflineBattleOutcome> battles,
            long remainderMs = 0, OfflinePendingBattle? pendingBattle = null,
            int endingLevel = 0, int endingExp = 0)
        {
            Mode = mode;
            ZoneId = zoneId;
            Battles = battles;
            RemainderMs = remainderMs;
            PendingBattle = pendingBattle;
            EndingLevel = endingLevel;
            EndingExp = endingExp;

            // Fold the per-battle outcomes into the run-level aggregates in a single pass, keeping the
            // outcome list the one source of truth (the summary is a materialized view of it).
            foreach (var battle in battles)
            {
                if (battle.Result.Victory)
                {
                    Wins++;
                    TotalExp += battle.ExpReward;
                }
                else if (battle.Result.PlayerDied)
                {
                    Losses++;
                }
                else
                {
                    Draws++;
                }
            }
        }
    }
}
