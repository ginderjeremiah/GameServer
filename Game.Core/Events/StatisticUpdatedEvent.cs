namespace Game.Core.Events
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
