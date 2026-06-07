using Game.Abstractions.Entities;
using CoreItemMod = Game.Core.Items.ItemMod;

namespace Game.Abstractions.DataAccess
{
    public interface IItemMods
    {
        public void InvalidateCache();
        public List<ItemMod> All(bool refreshCache = false);
        public Dictionary<int, IEnumerable<ItemMod>> GetModsForItemByType(int itemId);
        public ItemMod? LookupItemMod(int itemModId);
        public CoreItemMod GetItemMod(int itemModId);
    }
}
