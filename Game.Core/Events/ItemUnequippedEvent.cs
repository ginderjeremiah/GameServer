namespace Game.Core.Events
{
    /// <summary>
    /// Raised when a player unequips an item from a slot.
    /// </summary>
    public record ItemUnequippedEvent(
        int PlayerId,
        int ItemId) : IDomainEvent;
}
