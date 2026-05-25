using Game.Abstractions.DataAccess;
using Game.Core;

namespace Game.Application.Services
{
    public class StatisticsService(IPlayerStatistics playerStatistics)
    {
        private readonly IPlayerStatistics _playerStatistics = playerStatistics;

        /// <summary>
        /// Increments a player statistic and returns the new value.
        /// </summary>
        public async Task<long> RecordStatistic(int playerId, EStatisticType type, int entityId, long amount)
        {
            return await _playerStatistics.IncrementStatistic(playerId, (int)type, entityId, amount);
        }
    }
}
