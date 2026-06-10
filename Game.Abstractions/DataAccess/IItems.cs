using Contracts = Game.Abstractions.Contracts;
using CoreItem = Game.Core.Items.Item;

namespace Game.Abstractions.DataAccess
{
    public interface IItems
    {
        public List<Contracts.Item> All();
        public CoreItem GetItem(int itemId);
    }
}
