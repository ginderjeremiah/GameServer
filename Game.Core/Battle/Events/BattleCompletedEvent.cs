using Game.Core.Events;
using Game.Core.Players;

namespace Game.Core.Battle.Events
{
    public record BattleCompletedEvent(
        Player Player,
        int EnemyId,
        bool Victory,
        bool PlayerDied,
        int TotalMs,
        BattleStats Stats) : IDomainEvent;
}
