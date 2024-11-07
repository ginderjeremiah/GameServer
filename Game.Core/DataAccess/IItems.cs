using Game.Core.Entities;

namespace Game.Core.DataAccess
{
    public interface IItems
    {
        public List<Item> All(bool refreshCache = false);
        public Item? GetItem(int itemId);
    }
}
