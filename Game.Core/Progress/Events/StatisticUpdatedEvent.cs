using Game.Core.Events;

namespace Game.Core.Progress.Events
{
    /// <summary>
    /// Raised when a player statistic is updated.
    /// </summary>
    public record StatisticUpdatedEvent(
        int PlayerId,
        EStatisticType Type,
        int EntityId,
        long NewValue) : IDomainEvent;
}
