using Game.Abstractions.Entities;

namespace Game.Abstractions.DataAccess
{
    public interface IPlayerStatistics
    {
        public Task<List<PlayerStatistic>> GetPlayerStatistics(int playerId);
        public Task<long> IncrementStatistic(int playerId, int statisticTypeId, int? entityId, long amount);
        public Task<long> SetMaxStatistic(int playerId, int statisticTypeId, int? entityId, long value);
        public Task<long> SetMinStatistic(int playerId, int statisticTypeId, int? entityId, long value);
    }
}
