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
        // The player's power for this battle (DefeatRewards.PlayerPower — the sum of core additive attribute
        // modifiers), carried so the progress handler can normalize each path's activity by it for the
        // effect-based proficiency accrual (spike #1318). Meaningful only on a victory — XP accrues on wins —
        // so the loss/abandon paths leave it at the default, and a default of 0 yields no accrual (the
        // power-normalization guard treats non-positive power as no claim).
        double PlayerPower = 0) : IDomainEvent, ILoggableDomainEvent
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
