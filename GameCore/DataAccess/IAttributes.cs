using Attribute = GameCore.Entities.Attribute;

namespace GameCore.DataAccess
{
    public interface IAttributes
    {
        public IQueryable<Attribute> AllAttributes();
    }
}
