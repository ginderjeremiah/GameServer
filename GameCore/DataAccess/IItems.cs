using GameCore.Entities;

namespace GameCore.DataAccess
{
    public interface IItems
    {
        public List<Item> AllItems(bool refreshCache = false);
        public Item? GetItem(int itemId);
    }
}
