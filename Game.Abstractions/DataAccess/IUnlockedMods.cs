using Game.Abstractions.Entities;

namespace Game.Abstractions.DataAccess
{
    public interface IUnlockedMods
    {
        public Task<List<UnlockedMod>> GetUnlockedMods(int playerId);
        public Task UnlockMod(int playerId, int itemModId);
    }
}
