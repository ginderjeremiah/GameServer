using Game.Abstractions.Entities;

namespace Game.Abstractions.DataAccess
{
    public interface IItems
    {
        public List<Item> All(bool refreshCache = false);
        public Item? GetItem(int itemId);
    }
}
