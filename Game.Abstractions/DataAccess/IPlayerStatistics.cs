using Game.Abstractions.Entities;

namespace Game.Abstractions.DataAccess
{
    public interface IPlayerStatistics
    {
        public Task<List<PlayerStatistic>> GetPlayerStatistics(int playerId);
        public Task<long> IncrementStatistic(int playerId, int statisticTypeId, int entityId, long amount);
    }
}
