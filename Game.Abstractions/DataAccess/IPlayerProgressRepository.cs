using Game.Core.Players;
using Game.Core.Progress;

namespace Game.Abstractions.DataAccess
{
    public interface IPlayerProgressRepository
    {
        Task<PlayerProgress> Load(Player player);
        Task Save(PlayerProgress progress);

        /// <summary>
        /// Returns the player's tracked statistics as domain models for read-only consumers.
        /// </summary>
        Task<List<PlayerStatistic>> GetStatistics(int playerId);

        /// <summary>
        /// Returns the player's challenge progress as domain models for read-only consumers.
        /// </summary>
        Task<List<PlayerChallenge>> GetChallenges(int playerId);
    }
}
