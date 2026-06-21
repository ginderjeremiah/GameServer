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

        /// <summary>The total simulated battle time, in milliseconds, excluding the inter-battle cooldown
        /// gaps.</summary>
        public long TotalBattleMs { get; }

        /// <summary>Per-enemy victory (kill) counts, keyed by enemy id. Only victories contribute, mirroring
        /// the live <c>EnemiesKilled</c> statistic.</summary>
        public IReadOnlyDictionary<int, int> EnemyKillCounts { get; }

        public OfflineProgressResult(OfflineLoopMode mode, int zoneId, IReadOnlyList<OfflineBattleOutcome> battles)
        {
            Mode = mode;
            ZoneId = zoneId;
            Battles = battles;

            // Fold the per-battle outcomes into the run-level aggregates in a single pass, keeping the
            // outcome list the one source of truth (the summary is a materialized view of it).
            var killCounts = new Dictionary<int, int>();
            foreach (var battle in battles)
            {
                TotalBattleMs += battle.Result.TotalMs;

                if (battle.Result.Victory)
                {
                    Wins++;
                    TotalExp += battle.ExpReward;
                    killCounts[battle.Enemy.Id] = killCounts.GetValueOrDefault(battle.Enemy.Id) + 1;
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

            EnemyKillCounts = killCounts;
        }
    }
}
