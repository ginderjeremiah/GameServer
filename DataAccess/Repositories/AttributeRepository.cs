using GameCore.DataAccess;
using GameCore.Infrastructure;
using Attribute = GameCore.Entities.Attributes.Attribute;

namespace DataAccess.Repositories
{
    internal class Attributes : BaseRepository, IAttributes
    {
        public Attributes(IDatabaseService database) : base(database) { }

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
}
