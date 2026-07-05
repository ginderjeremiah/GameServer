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
        /// The trailing remainder left once the away-window crediting loop exhausts the budget (#1596): the
        /// last credited battle+cooldown cycle's assumed duration runs past the real away-window boundary by
        /// this many milliseconds (0 when nothing was simulated, or when the stalemate cutoff — a CPU-waste
        /// guard, not an overshoot — stopped the loop early with genuine unspent budget). The orchestration
        /// layer carries this forward as either a residual cooldown or the elapsed offset of a fresh
        /// already-in-progress next battle, rather than dropping it when the live loop resumes.
        /// </summary>
        public long RemainderMs { get; }

        public OfflineProgressResult(
            OfflineLoopMode mode, int zoneId, IReadOnlyList<OfflineBattleOutcome> battles, long remainderMs = 0)
        {
            Mode = mode;
            ZoneId = zoneId;
            Battles = battles;
            RemainderMs = remainderMs;

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
