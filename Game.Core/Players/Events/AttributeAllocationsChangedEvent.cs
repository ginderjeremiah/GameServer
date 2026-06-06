using Game.Core.Events;

namespace Game.Core.Players.Events
{
    /// <summary>
    /// Raised when a player's stat point allocations change.
    /// </summary>
    public record AttributeAllocationsChangedEvent(
        int PlayerId,
        List<AttributeAllocationEntry> Allocations) : IDomainEvent;

    public record AttributeAllocationEntry(EAttribute Attribute, double Amount);
}
