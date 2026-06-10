using Game.Core.Events;

namespace Game.Core.Players.Events
{
    /// <summary>
    /// Raised when a player replaces their equipped skill loadout. Carries the full ordered equipped
    /// set; the write-behind handler rebuilds the player's <c>Selected</c>/<c>Order</c> columns from it
    /// (delete-then-rebuild, idempotent).
    /// </summary>
    public record SelectedSkillsChangedEvent(
        int PlayerId,
        List<int> OrderedSkillIds) : IDomainEvent;
}
