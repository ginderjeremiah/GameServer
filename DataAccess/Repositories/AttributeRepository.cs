using Attribute = DataAccess.Models.Attributes.Attribute;

namespace DataAccess.Repositories
{
    internal class Attributes : BaseRepository, IAttributes
    {
        public Attributes(string connectionString) : base(connectionString) { }

        public List<Attribute> AllAttributes()
        {
            var commandText = @"
                SELECT
                    AttributeId,
                    AttributeName,
                    AttributeDesc
                FROM Attributes
                ORDER BY AttributeId";

            return QueryToList<Attribute>(commandText);
        }
    }

    public interface IAttributes
    {
        public List<Attribute> AllAttributes();
    }
}
