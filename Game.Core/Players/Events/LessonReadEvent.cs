using Game.Core.Events;

namespace Game.Core.Players.Events
{
    /// <summary>
    /// Raised when a lesson's coach-mark tour is completed. Carries <see cref="UnlockedAt"/> alongside
    /// <see cref="ReadAt"/> because a screen-anchored lesson plays immediately on first visit with no prior
    /// <see cref="LessonUnlockedEvent"/> — the handler needs both timestamps to insert the row if this is its
    /// first write.
    /// </summary>
    public record LessonReadEvent(
        int PlayerId,
        int LessonId,
        DateTime UnlockedAt,
        DateTime ReadAt) : IDomainEvent;
}
