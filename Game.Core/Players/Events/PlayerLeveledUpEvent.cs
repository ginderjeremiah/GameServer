using Game.Core.Events;

namespace Game.Core.Players.Events
{
    /// <summary>
    /// Raised when a player gains enough experience to reach the next level.
    /// </summary>
    /// <param name="Player">The player who leveled up.</param>
    /// <param name="NewLevel">The level the player reached.</param>
    /// <param name="StatPointsGained">Cumulative stat points the player has earned so far.</param>
    public record PlayerLeveledUpEvent(Player Player, int NewLevel, int StatPointsGained)
        : IDomainEvent, ILoggableDomainEvent
    {
        // Curated safe scalars only — never the Player aggregate, stats, or inventory.
        public IReadOnlyList<KeyValuePair<string, object?>> GetLogProperties() =>
        [
            new("PlayerId", Player.Id),
            new("NewLevel", NewLevel),
            new("StatPointsGained", StatPointsGained),
        ];
    }
}
