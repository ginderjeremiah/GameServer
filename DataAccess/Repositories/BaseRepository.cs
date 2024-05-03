using GameLibrary.Database.Interfaces;

namespace DataAccess.Repositories
{
    internal class BaseRepository
    {
        protected IDataProvider Database { get; set; }

        protected BaseRepository(IDataProvider database)
        {
            Database = database;
        }
    }
}
