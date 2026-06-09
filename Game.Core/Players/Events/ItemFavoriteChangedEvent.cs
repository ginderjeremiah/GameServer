using Game.Core.Events;

namespace Game.Core.Players.Events
{
    /// <summary>
    /// Raised when a player favorites or unfavorites an unlocked item.
    /// </summary>
    public record ItemFavoriteChangedEvent(
        int PlayerId,
        int ItemId,
        bool Favorite) : IDomainEvent;
}
