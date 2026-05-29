using Game.Core.Events;
using Game.Core.Players;

namespace Game.Core.Battle.Events
{
    /// <summary>
    /// Raised when a player successfully defeats an enemy.
    /// </summary>
    public record EnemyDefeatedEvent(
        Player Player,
        int EnemyId,
        int ExpReward) : IDomainEvent;
}
