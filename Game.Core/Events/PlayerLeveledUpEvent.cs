namespace Game.Core.Events
{
    /// <summary>
    /// Raised when a player gains enough experience to reach the next level.
    /// </summary>
    /// <param name="PlayerId">The player who leveled up.</param>
    /// <param name="NewLevel">The level the player reached.</param>
    /// <param name="StatPointsGained">Cumulative stat points the player has earned so far.</param>
    public record PlayerLeveledUpEvent(int PlayerId, int NewLevel, int StatPointsGained) : IDomainEvent;
}
