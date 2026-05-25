namespace Game.Core.Events
{
    /// <summary>
    /// Raised when a modifier is removed from an item's mod slot.
    /// </summary>
    public record ModRemovedEvent(
        int PlayerId,
        int ItemId,
        int ItemModSlotId) : IDomainEvent;
}
