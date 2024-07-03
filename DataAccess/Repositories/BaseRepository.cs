using GameInfrastructure.Database;

namespace DataAccess.Repositories
{
    internal class BaseRepository
    {
        protected GameContext Database { get; set; }

        protected BaseRepository(GameContext database)
        {
            Database = database;
        }
    }
}
