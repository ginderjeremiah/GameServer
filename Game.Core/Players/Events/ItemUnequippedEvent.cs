namespace Game.Core.Players.Events
{
    /// <summary>
    /// Raised when a player unequips an item from a slot.
    /// </summary>
    public record ItemUnequippedEvent(
        int PlayerId,
        int ItemId) : IPlayerPersistenceEvent;
}
