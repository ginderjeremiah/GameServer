using Game.Core.Players;
using Game.Core.Progress;

namespace Game.Abstractions.DataAccess
{
    public interface IPlayerProgressRepository
    {
        Task<PlayerProgress> Load(Player player, CancellationToken cancellationToken = default);
        Task Save(PlayerProgress progress, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns the player's tracked statistics as domain models for read-only consumers.
        /// </summary>
        Task<List<PlayerStatistic>> GetStatistics(int playerId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns the player's challenge progress as domain models for read-only consumers.
        /// </summary>
        Task<List<PlayerChallenge>> GetChallenges(int playerId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns the ids of the challenges the player has completed. A lean read for gating checks (e.g.
        /// zone-unlock enforcement) that only need completion, not full progress.
        /// </summary>
        Task<HashSet<int>> GetCompletedChallengeIds(int playerId, CancellationToken cancellationToken = default);
    }
}
