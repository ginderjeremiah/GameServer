using GameCore.Infrastructure;
using GameInfrastructure.Database.SqlServer;

namespace GameInfrastructure.Database
{
    internal static class DatabaseServiceFactory
    {
        public static IDatabaseService GetDatabaseService(IDatabaseConfiguration config)
        {
            return config.DatabaseSystem switch
            {
                DatabaseSystem.SqlServer => new SqlServerService(config),
                _ => new SqlServerService(config)
            };
        }
    }
}
