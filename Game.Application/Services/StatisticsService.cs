using Game.Abstractions.DataAccess;
using Game.Core;

namespace Game.Application.Services
{
    public class StatisticsService(IPlayerRepository playerRepo)
    {
        private readonly IPlayerRepository _playerRepo = playerRepo;

        /// <summary>
        /// Increments a player statistic and returns the new value.
        /// </summary>
        public async Task<long> RecordStatistic(int playerId, EStatisticType type, int entityId, long amount)
        {
            return await _playerRepo.IncrementStatistic(playerId, (int)type, entityId, amount);
        }
    }
}
