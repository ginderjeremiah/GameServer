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
        // The player's combat rating for this battle (DefeatRewards.PlayerRating), carried so the progress
        // handler can max-normalize each path's activity against it for the effect-based proficiency accrual
        // (spike #1526 Decision 5). Meaningful only on a victory — XP accrues on wins — so the loss/abandon
        // paths leave it at the default, and a default of 0 yields no accrual (the normalization guard treats
        // a non-positive normalizer as no claim).
        double PlayerRating = 0) : IDomainEvent, ILoggableDomainEvent
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
