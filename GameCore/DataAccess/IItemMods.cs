using GameCore.Entities;

namespace GameCore.DataAccess
{
    public interface IItemMods
    {
        public List<ItemMod> AllItemMods(bool refreshCache = false);
        public Dictionary<int, IEnumerable<ItemMod>> GetModsForItemBySlot(int itemId);
        public ItemMod? GetItemMod(int itemModId);
    }
}
