using Game.Core.Entities;

namespace Game.Core.DataAccess
{
    public interface IItemMods
    {
        public List<ItemMod> All(bool refreshCache = false);
        public Dictionary<int, IEnumerable<ItemMod>> GetModsForItemByType(int itemId);
        public ItemMod? GetItemMod(int itemModId);
    }
}
