using GameCore.DataAccess;
using GameInfrastructure.Database;
using Attribute = GameCore.Entities.Attribute;

namespace DataAccess.Repositories
{
    internal class Attributes : BaseRepository, IAttributes
    {
        public Attributes(GameContext database) : base(database) { }

        public IQueryable<Attribute> AllAttributes()
        {
            return Database.Attributes;
        }
    }
}
