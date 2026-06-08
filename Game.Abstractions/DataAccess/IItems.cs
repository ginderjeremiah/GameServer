using Contracts = Game.Abstractions.Contracts;
using CoreItem = Game.Core.Items.Item;

namespace Game.Abstractions.DataAccess
{
    public interface IItems
    {
        public void InvalidateCache();
        public List<Contracts.Item> All(bool refreshCache = false);
        public CoreItem GetItem(int itemId);
    }
}
