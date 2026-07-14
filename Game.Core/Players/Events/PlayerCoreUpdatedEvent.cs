using Game.Core.Events;

namespace Game.Core.Players.Events
{
    /// <summary>
    /// Raised when a player's core fields change (level, exp, zone, stat points, last-activity anchor,
    /// the persisted idle-loop mode / auto-challenge-boss flag, and the last durably-credited battle seed).
    /// </summary>
    public record PlayerCoreUpdatedEvent(
        int PlayerId,
        int Level,
        int Exp,
        int CurrentZoneId,
        int StatPointsGained,
        int StatPointsUsed,
        DateTime LastActivity,
        bool AutoChallengeBoss,
        uint? LastCreditedBattleSeed = null) : IDomainEvent;
}
