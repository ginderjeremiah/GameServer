using Game.Core.Items;

namespace Game.Core.Events
{
    /// <summary>
    /// Raised when a player successfully defeats an enemy and collects rewards.
    /// </summary>
    /// <param name="PlayerId">The player who defeated the enemy.</param>
    /// <param name="EnemyId">The defeated enemy's identifier.</param>
    /// <param name="ExpReward">Experience points awarded for the defeat.</param>
    /// <param name="DroppedItems">Items that dropped from the enemy.</param>
    public record EnemyDefeatedEvent(
        int PlayerId,
        int EnemyId,
        int ExpReward,
        IReadOnlyList<Item> DroppedItems) : IDomainEvent;
}
