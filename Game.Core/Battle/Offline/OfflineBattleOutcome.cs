using Game.Core.Enemies;

namespace Game.Core.Battle.Offline
{
    /// <summary>
    /// The outcome of a single simulated offline battle, retained so the orchestration layer (the offline
    /// reward-application sub-issue) can grant exp per victory and feed each battle's stats through the same
    /// per-battle recording path the live battle-completion handler uses (rather than re-deriving the stat
    /// logic — see spike #879, decision 7). The simulator computes <see cref="ExpReward"/> from the player's
    /// (stationary) snapshot at simulation time so the value matches the battle it was earned in; it is
    /// <c>0</c> for a non-victory.
    /// </summary>
    public record OfflineBattleOutcome(Enemy Enemy, BattleResult Result, int ExpReward);
}
