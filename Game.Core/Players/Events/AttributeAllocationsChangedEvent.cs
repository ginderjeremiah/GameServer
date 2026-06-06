namespace Game.Core.Players.Events
{
    /// <summary>
    /// Raised when a player's stat point allocations change.
    /// </summary>
    public record AttributeAllocationsChangedEvent(
        int PlayerId,
        List<AttributeAllocationEntry> Allocations) : IPlayerPersistenceEvent;

    public record AttributeAllocationEntry(EAttribute Attribute, double Amount);
}
