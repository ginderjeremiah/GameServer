using Game.Core.Enemies;

namespace Game.Core.Battle.Offline
{
    /// <summary>
    /// The battle straddling the away-window boundary, when the boundary falls <em>inside</em> it rather than
    /// in a completed battle's post-battle cooldown (#1596): the away budget ran out before this battle's own
    /// (deterministic, already-simulated) duration would have elapsed, so it is not a completed outcome and is
    /// not credited — carrying it forward as this record instead lets the orchestration hand it back already
    /// active, exactly as it was simulated (same enemy/seed), with its real elapsed offset. Mirrors the
    /// leading-edge stale-battle hand-back (#1595): this is simply that same "still in progress" state, arrived
    /// at from the trailing edge of the away window instead of a mid-battle disconnect.
    /// </summary>
    public sealed record OfflinePendingBattle(Enemy Enemy, uint Seed, int ElapsedOffsetMs);
}
