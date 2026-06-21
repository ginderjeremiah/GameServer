using Game.Core.Events;

namespace Game.Core.Players.Events
{
    /// <summary>
    /// Raised when a player's core fields change (level, exp, zone, stat points, last-activity anchor, and
    /// the persisted idle-loop mode / auto-challenge-boss zone).
    /// </summary>
    public record PlayerCoreUpdatedEvent(
        int PlayerId,
        int Level,
        int Exp,
        int CurrentZoneId,
        int StatPointsGained,
        int StatPointsUsed,
        DateTime LastActivity,
        int? AutoChallengeBossZoneId) : IDomainEvent;
}
