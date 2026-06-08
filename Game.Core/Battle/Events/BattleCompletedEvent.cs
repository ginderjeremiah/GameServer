using Game.Core.Enemies;
using Game.Core.Events;
using Game.Core.Players;

namespace Game.Core.Battle.Events
{
    public record BattleCompletedEvent(
        Player Player,
        Enemy Enemy,
        bool Victory,
        bool PlayerDied,
        int TotalMs,
        BattleStats Stats,
        bool IsBossBattle,
        int ZoneId) : IDomainEvent;
}
