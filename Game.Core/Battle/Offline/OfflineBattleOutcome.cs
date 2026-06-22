using Game.Core.Enemies;

namespace Game.Core.Battle.Offline
{
    /// <summary>
    /// The outcome of a single simulated offline battle, retained so the orchestration layer (the offline
    /// reward-application sub-issue) can grant exp per victory and feed each battle's stats through the same
    /// per-battle recording path the live battle-completion handler uses (rather than re-deriving the stat
    /// logic — see spike #879, decision 7). The simulator computes <see cref="ExpReward"/> and
    /// <see cref="DifficultyMultiplier"/> from the player's (stationary) snapshot at simulation time so the
    /// values match the battle they were earned in; both are <c>0</c> for a non-victory.
    /// <para>
    /// <see cref="DifficultyMultiplier"/> is the <c>DefeatRewards</c> difficulty factor the offline proficiency
    /// XP accrual scales its fixed pie by — the same input the live path threads through
    /// <c>BattleCompletedEvent</c>, so offline and live accrue identically (spike #982).
    /// </para>
    /// </summary>
    public record OfflineBattleOutcome(Enemy Enemy, BattleResult Result, int ExpReward, double DifficultyMultiplier);
}
