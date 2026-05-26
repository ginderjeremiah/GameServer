using Game.Abstractions.Entities;

namespace Game.Abstractions.DataAccess
{
    public interface IItems
    {
        public void InvalidateCache();
        public List<Item> All(bool refreshCache = false);
        public Item? LookupItem(int itemId);
        public Item GetItem(int itemId);
    }
}
