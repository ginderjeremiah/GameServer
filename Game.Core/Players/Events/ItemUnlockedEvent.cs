namespace Game.Core.Players.Events
{
    /// <summary>
    /// Raised when a player unlocks a new item.
    /// </summary>
    public record ItemUnlockedEvent(
        int PlayerId,
        int ItemId) : IPlayerPersistenceEvent;
}
