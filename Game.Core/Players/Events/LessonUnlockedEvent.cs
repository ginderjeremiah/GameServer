using Game.Core.Events;

namespace Game.Core.Players.Events
{
    /// <summary>
    /// Raised when a mechanic-anchored lesson's trigger fires (client-detected, trusted).
    /// </summary>
    public record LessonUnlockedEvent(
        int PlayerId,
        int LessonId,
        DateTime UnlockedAt) : IDomainEvent;
}
