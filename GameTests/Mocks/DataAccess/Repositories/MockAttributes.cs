using GameCore.DataAccess;
using Attribute = GameCore.Entities.Attributes.Attribute;

namespace GameTests.Mocks.DataAccess.Repositories
{
    internal class MockAttributes : IAttributes
    {
        public List<Attribute> Attributes { get; set; } = new();
        public List<Attribute> AllAttributes()
        {
            return Attributes;
        }
    }
}
