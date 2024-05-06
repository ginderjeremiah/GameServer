using Attribute = GameCore.Entities.Attributes.Attribute;

namespace GameCore.DataAccess
{
    public interface IAttributes
    {
        public List<Attribute> AllAttributes();
    }
}
