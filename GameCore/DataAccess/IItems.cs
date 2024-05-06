using GameCore.Entities.Items;

namespace GameCore.DataAccess
{
    public interface IItems
    {
        public List<Item> AllItems(bool refreshCache = false);
        public void AddItem(string itemName, string itemDesc, int itemCategoryId, string iconPath);
        public void UpdateItem(int itemId, string itemName, string itemDesc, int itemCategoryId, string iconPath);
        public void DeleteItem(int itemId);
    }
}
