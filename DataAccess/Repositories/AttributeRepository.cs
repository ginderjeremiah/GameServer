using GameCore.DataAccess;
using GameCore.Infrastructure;
using Attribute = GameCore.Entities.Attribute;

namespace DataAccess.Repositories
{
    internal class Attributes : BaseRepository, IAttributes
    {
        public Attributes(IDatabaseService database) : base(database) { }

        public IQueryable<Attribute> AllAttributes()
        {
            return Database.Attributes;
        }
    }
}
