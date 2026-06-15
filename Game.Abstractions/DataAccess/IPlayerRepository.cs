using Game.Core.Players;

namespace Game.Abstractions.DataAccess
{
    /// <summary>
    /// Repository for the Player aggregate. Uses write-behind caching:
    /// reads go through Redis, writes update Redis immediately and queue
    /// async DB sync via domain events.
    /// </summary>
    public interface IPlayerRepository
    {
        Task<Player?> GetPlayer(int playerId, CancellationToken cancellationToken = default);
        Task SavePlayer(Player player, CancellationToken cancellationToken = default);
    }
}
