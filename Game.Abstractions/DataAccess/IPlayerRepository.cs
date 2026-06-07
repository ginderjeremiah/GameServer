using Game.Core.Players;
using UserEntity = Game.Abstractions.Entities.User;

namespace Game.Abstractions.DataAccess
{
    /// <summary>
    /// Repository for the Player aggregate. Uses write-behind caching:
    /// reads go through Redis, writes update Redis immediately and queue
    /// async DB sync via domain events.
    /// </summary>
    public interface IPlayerRepository
    {
        Task<Player?> GetPlayer(int playerId);
        Task SavePlayer(Player player);

        /// <summary>
        /// Persists a brand-new player (built from the <see cref="NewPlayer"/> blueprint) for the
        /// given <paramref name="user"/> by queuing its entity graph in the surrounding unit of work.
        /// This is the initial-creation path: unlike <see cref="SavePlayer"/> it goes straight to the
        /// database rather than through the write-behind cache, since a freshly created player has no
        /// cached state or domain events yet.
        /// </summary>
        void CreatePlayer(UserEntity user, NewPlayer newPlayer);
    }
}
