using GameCore.DataAccess;
using GameCore.Entities;

namespace GameTests.Mocks.DataAccess.Repositories
{
    internal class MockItems : IItems
    {
        public List<Item> Items { get; set; } = new();
        public bool Refreshed { get; set; } = false;

        public void AddItem(string itemName, string itemDesc, int itemCategoryId, string iconPath)
        {
            var maxId = Items.Max(item => item.ItemId) + 1;
            Items.Add(new Item
            {
                ItemId = maxId,
                ItemName = itemName,
                ItemDesc = itemDesc,
                ItemCategoryId = itemCategoryId,
                IconPath = iconPath
            });
        }

        public List<Item> AllItems(bool refreshCache = false)
        {
            Refreshed = refreshCache;
            return Items;
        }

        public void DeleteItem(int itemId)
        {
            var itemToRemove = Items.FirstOrDefault(item => item.ItemId == itemId);
            if (itemToRemove != null)
            {
                Items.Remove(itemToRemove);
            }
        }

        public void UpdateItem(int itemId, string itemName, string itemDesc, int itemCategoryId, string iconPath)
        {
            var itemToUpdate = Items.FirstOrDefault(item => item.ItemId == itemId);
            if (itemToUpdate != null)
            {
                itemToUpdate.ItemName = itemName;
                itemToUpdate.ItemDesc = itemDesc;
                itemToUpdate.ItemCategoryId = itemCategoryId;
                itemToUpdate.IconPath = iconPath;
            }
        }
    }
}
