using GameCore.Infrastructure;

namespace DataAccess.Repositories
{
    internal class BaseRepository
    {
        protected IDatabaseService Database { get; set; }

        protected BaseRepository(IDatabaseService database)
        {
            Database = database;
        }
    }
}
