using Game.Abstractions.Entities;

namespace Game.Abstractions.DataAccess
{
    public interface IUnlockedItems
    {
        public Task<List<UnlockedItem>> GetUnlockedItems(int playerId);
        public Task UnlockItem(int playerId, int itemId);
        public Task EquipItem(int playerId, int itemId, int equipmentSlotId);
        public Task UnequipItem(int playerId, int itemId);
    }
}
