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
        // The combatants' combat ratings for this battle (DefeatRewards.PlayerRating/EnemyRating, spike #1526),
        // carried so the progress handler can normalize each path's activity by max(PlayerRating, EnemyRating)
        // for the effect-based proficiency accrual (spike #1526 Decision 5, #1532). Meaningful only on a
        // victory — XP accrues on wins — so the loss/abandon paths leave both at the default, and a default of
        // 0/0 yields no accrual (the normalization guard treats a non-positive denominator as no claim).
        double PlayerRating = 0, double EnemyRating = 0) : IDomainEvent, ILoggableDomainEvent
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
