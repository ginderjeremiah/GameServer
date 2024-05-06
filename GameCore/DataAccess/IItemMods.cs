using GameCore.Entities.ItemMods;

namespace GameCore.DataAccess
{
    public interface IItemMods
    {
        public List<ItemMod> AllItemMods(bool refreshCache = false);
        public Dictionary<int, List<ItemModWithoutAttributes>> GetModsForItemBySlot(int itemId);
        public void AddItemMod(string itemModName, bool removable, string itemModDesc, int slotTypeId);
        public void UpdateItemMod(int itemModId, string itemModName, bool removable, string itemModDesc, int slotTypeId);
        public void DeleteItemMod(int itemModId);
    }
}
