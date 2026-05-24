namespace Game.Core.Events
{
    /// <summary>
    /// Raised when a player successfully defeats an enemy.
    /// </summary>
    public record EnemyDefeatedEvent(
        int PlayerId,
        int EnemyId,
        int ExpReward) : IDomainEvent;
}
