using GameCore.Database.Interfaces;
using Attribute = DataAccess.Entities.Attributes.Attribute;

namespace DataAccess.Repositories
{
    internal class Attributes : BaseRepository, IAttributes
    {
        public Attributes(IDataProvider database) : base(database) { }

        public List<Attribute> AllAttributes()
        {
            var commandText = @"
                SELECT
                    AttributeId,
                    AttributeName,
                    AttributeDesc
                FROM Attributes
                ORDER BY AttributeId";

            return Database.QueryToList<Attribute>(commandText);
        }
    }

    public interface IAttributes
    {
        public List<Attribute> AllAttributes();
    }
}
