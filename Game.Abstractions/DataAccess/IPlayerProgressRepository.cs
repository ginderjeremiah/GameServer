using Game.Core.Players;
using Game.Core.Progress;

namespace Game.Abstractions.DataAccess
{
    public interface IPlayerProgressRepository
    {
        Task<PlayerProgress> Load(Player player);
        void Save(PlayerProgress progress);
    }
}
