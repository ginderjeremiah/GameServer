using DataAccess.Entities.ItemMods;
using DataAccess.Repositories;

namespace GameTests.Mocks.DataAccess.Repositories
{
    internal class MockItemMods : IItemMods
    {
        public void AddItemMod(string itemModName, bool removable, string itemModDesc)
        {
            throw new NotImplementedException();
        }

        public List<ItemMod> AllItemMods(bool refreshCache = false)
        {
            throw new NotImplementedException();
        }

        public void DeleteItemMod(int itemModId)
        {
            throw new NotImplementedException();
        }

        public Dictionary<int, List<ItemModWithoutAttributes>> GetModsForItemBySlot(int itemId)
        {
            throw new NotImplementedException();
        }

        public void UpdateItemMod(int itemModId, string itemModName, bool removable, string itemModDesc)
        {
            throw new NotImplementedException();
        }
    }
}
