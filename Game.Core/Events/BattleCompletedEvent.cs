using Game.Core.Battle;

namespace Game.Core.Events
{
    public record BattleCompletedEvent(
        int PlayerId,
        int EnemyId,
        bool Victory,
        bool PlayerDied,
        int TotalMs,
        BattleStats Stats) : IDomainEvent;
}
