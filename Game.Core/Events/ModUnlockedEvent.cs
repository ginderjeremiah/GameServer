namespace Game.Core.Events
{
    /// <summary>
    /// Raised when a player unlocks a new item modifier.
    /// </summary>
    public record ModUnlockedEvent(
        int PlayerId,
        int ItemModId) : IDomainEvent;
}
