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
            int endingLevel = 0, int endingExp = 0)
        {
            Mode = mode;
            ZoneId = zoneId;
            Battles = battles;
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
