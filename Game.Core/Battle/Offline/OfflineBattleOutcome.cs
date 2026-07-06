using Game.Core.Enemies;
using Game.Core.Proficiencies;

namespace Game.Core.Battle.Offline
{
    /// <summary>
    /// The outcome of a single simulated offline battle, retained so the orchestration layer (the offline
    /// reward-application sub-issue) can grant exp per victory and feed each battle's stats through the same
    /// per-battle recording path the live battle-completion handler uses (rather than re-deriving the stat
    /// logic — see spike #879, decision 7). The simulator computes <see cref="ExpReward"/>,
    /// <see cref="PlayerRating"/> and <see cref="EnemyRating"/> from the player's snapshot and the battle's
    /// enemy at simulation time — the snapshot's level (and through it the class locked base) grows mid-window
    /// as victories are earned (#1601), so these values reflect the battle's own point in that growth, not a
    /// single frozen power — and are <c>0</c> for a non-victory.
    /// <para>
    /// <see cref="PlayerRating"/>/<see cref="EnemyRating"/> are the <c>DefeatRewards</c> combat-rating measures
    /// (spike #1526) the offline effect-based proficiency accrual normalizes each path's activity by
    /// (<c>max(PlayerRating, EnemyRating)</c>) — the same inputs the live path threads through
    /// <c>BattleCompletedEvent</c>, so offline and live accrue identically (spike #1318, #1526 Decision 5).
    /// </para>
    /// <para>
    /// <see cref="ProficiencyGains"/> is this victory's proficiency accrual (<see cref="ProficiencyAccrual"/>),
    /// computed in-loop against the run's own working proficiency state (#1602) rather than post-hoc — so a
    /// later battle's snapshot already reflects a milestone this one crossed. Empty for a non-victory. The
    /// orchestration layer folds it (via <see cref="ProficiencyGainAccumulator"/>) into the window's applied
    /// proficiency progress rather than re-running the accrual.
    /// </para>
    /// </summary>
    public record OfflineBattleOutcome(
        Enemy Enemy, BattleResult Result, int ExpReward, double PlayerRating, double EnemyRating,
        ProficiencyAccrualResult ProficiencyGains);
}
