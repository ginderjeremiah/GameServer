using GameLibrary.Database.Interfaces;
using GameLibrary.Database.SqlServer;

namespace GameLibrary.Database
{
    public static class DataProviderFactory
    {
        public static IDataProvider GetDataProvider(IDataConfiguration config)
        {
            return new SqlServerProvider(config);
        }
    }
}
