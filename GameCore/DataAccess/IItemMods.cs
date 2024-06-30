using GameCore.Entities;

namespace GameCore.DataAccess
{
    public interface IItemMods
    {
        public Task<IEnumerable<ItemMod>> AllItemModsAsync(bool refreshCache = false);
        public Dictionary<int, IEnumerable<ItemMod>> GetModsForItemBySlot(int itemId);
        public Task<ItemMod?> GetItemModAsync(int itemModId);
    }
}
