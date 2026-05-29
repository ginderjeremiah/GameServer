using Game.Abstractions.Entities;

namespace Game.Abstractions.DataAccess
{
    public interface IPlayerStatistics
    {
        public Task<List<PlayerStatistic>> GetPlayerStatistics(int playerId);
    }
}
