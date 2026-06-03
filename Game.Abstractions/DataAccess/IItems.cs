using Game.Abstractions.Entities;
using CoreItem = Game.Core.Items.Item;

namespace Game.Abstractions.DataAccess
{
    public interface IItems
    {
        public void InvalidateCache();
        public List<Item> All(bool refreshCache = false);
        public Item? LookupItem(int itemId);
        public CoreItem GetItem(int itemId);
    }
}
