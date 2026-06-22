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
        int ZoneId,
        // The battle's difficulty curve factor (DefeatRewards.DifficultyMultiplier), carried so the progress
        // handler can scale the fixed proficiency-XP pie by it (spike #982 decision 4). Meaningful only on a
        // victory — XP accrues on wins — so the loss/abandon paths leave it at the default.
        double DifficultyMultiplier = 0) : IDomainEvent, ILoggableDomainEvent
    {
        // Curated safe scalars only — never the Player/Enemy aggregates, stats, or inventory.
        public IReadOnlyList<KeyValuePair<string, object?>> GetLogProperties() =>
        [
            new("PlayerId", Player.Id),
            new("EnemyId", Enemy.Id),
            new("Victory", Victory),
            new("PlayerDied", PlayerDied),
            new("IsBossBattle", IsBossBattle),
            new("ZoneId", ZoneId),
        ];
    }
}
