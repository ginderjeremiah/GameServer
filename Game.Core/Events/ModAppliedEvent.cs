namespace Game.Core.Events
{
    /// <summary>
    /// Raised when a modifier is applied to an item's mod slot.
    /// </summary>
    public record ModAppliedEvent(
        int PlayerId,
        int ItemId,
        int ItemModSlotId,
        int ItemModId) : IDomainEvent;
}
