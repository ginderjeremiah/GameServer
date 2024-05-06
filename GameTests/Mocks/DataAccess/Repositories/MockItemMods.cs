using GameCore.DataAccess;
using GameCore.Entities.ItemMods;

namespace GameTests.Mocks.DataAccess.Repositories
{
    internal class MockItemMods : IItemMods
    {
        public List<ItemMod> ItemMods { get; set; } = new();
        public void AddItemMod(string itemModName, bool removable, string itemModDesc, int slotTypeId)
        {
            var nextId = ItemMods.Max(mod => mod.ItemModId) + 1;
            ItemMods.Add(new ItemMod
            {
                ItemModId = nextId,
                ItemModName = itemModName,
                Removable = removable,
                ItemModDesc = itemModDesc,
                SlotTypeId = slotTypeId
            });
        }

        public List<ItemMod> AllItemMods(bool refreshCache = false)
        {
            return ItemMods;
        }

        public void DeleteItemMod(int itemModId)
        {
            var modToRemove = ItemMods.FirstOrDefault(mod => mod.ItemModId == itemModId);
            if (modToRemove != null)
            {
                ItemMods.Remove(modToRemove);
            }
        }

        public Dictionary<int, List<ItemModWithoutAttributes>> GetModsForItemBySlot(int itemId)
        {
            throw new NotImplementedException();
        }

        public void UpdateItemMod(int itemModId, string itemModName, bool removable, string itemModDesc, int slotTypeId)
        {
            var modToUpdate = ItemMods.FirstOrDefault(mod => mod.ItemModId == itemModId);
            if (modToUpdate != null)
            {
                modToUpdate.ItemModName = itemModName;
                modToUpdate.Removable = removable;
                modToUpdate.ItemModDesc = itemModDesc;
                modToUpdate.SlotTypeId = slotTypeId;
            }
        }
    }
}
