using Game.Abstractions.Entities;

namespace Game.Abstractions.DataAccess
{
    public interface IAppliedMods
    {
        public Task<List<AppliedMod>> GetAppliedMods(int playerId);
        public Task ApplyMod(int playerId, int itemId, int itemModSlotId, int itemModId);
        public Task RemoveMod(int playerId, int itemId, int itemModSlotId);
    }
}
