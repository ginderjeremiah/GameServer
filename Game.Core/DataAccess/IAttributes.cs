using Attribute = Game.Core.Entities.Attribute;

namespace Game.Core.DataAccess
{
    public interface IAttributes
    {
        public IAsyncEnumerable<Attribute> All();
    }
}
