using GameCore;
using GameInfrastructure;
using GameInfrastructure.Cache;
using GameInfrastructure.Database;
using GameInfrastructure.PubSub;
using LogLevel = GameCore.LogLevel;

namespace GameServer
{
    public class Config : IDataServicesConfiguration
    {
        public DatabaseSystem DatabaseSystem { get; }
        public string DbConnectionString { get; }
        public CacheSystem CacheSystem { get; }
        public string CacheConnectionString { get; }
        public string PubSubConnectionString { get; }
        public PubSubSystem PubSubSystem { get; }
        public string HashPepper { get; }
        public LogLevel MinimumLevel { get; }
        public Config(IConfiguration configuration)
        {
            //connection strings and hashPepper are set in user secrets
            DbConnectionString = configuration["DbConnectionString"] ?? throw new Exception("Could not retrieve DB connection string.");
            CacheConnectionString = configuration["CacheConnectionString"] ?? throw new Exception("Could not retrieve Cache connection string.");
            PubSubConnectionString = configuration["PubSubConnectionString"] ?? throw new Exception("Could not retrieve PubSub connection string.");
            HashPepper = configuration["HashPepper"] ?? throw new Exception("Could not retrieve pepper for Hashing.");

            //appsettings fields
            DatabaseSystem = (DatabaseSystem)Enum.Parse(typeof(DatabaseSystem), configuration["DatabaseSystem"].AsString());
            CacheSystem = (CacheSystem)Enum.Parse(typeof(CacheSystem), configuration["CacheSystem"].AsString());
            PubSubSystem = (PubSubSystem)Enum.Parse(typeof(PubSubSystem), configuration["PubSubSystem"].AsString());
            MinimumLevel = Enum.Parse<LogLevel>(configuration["MinimumLogLevel"] ?? LogLevel.Info.ToString());
        }
    }
}
