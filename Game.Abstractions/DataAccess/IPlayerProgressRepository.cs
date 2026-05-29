using Game.Core.Statistics;

namespace Game.Abstractions.DataAccess
{
    public interface IPlayerProgressRepository
    {
        Task<PlayerProgress> Load(int playerId);
        void Save(PlayerProgress progress);
    }
}
