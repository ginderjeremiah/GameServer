namespace Game.Core.Events
{
    /// <summary>
    /// Raised when a player equips an item to a slot.
    /// </summary>
    public record ItemEquippedEvent(
        int PlayerId,
        int ItemId,
        int SlotId) : IDomainEvent;
}
