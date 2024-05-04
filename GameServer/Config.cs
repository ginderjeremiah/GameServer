using GameCore.Database.Interfaces;
using GameCore.Logging.Interfaces;
using LogLevel = GameCore.Logging.LogLevel;

namespace GameServer
{
    public class Config : IDataConfiguration, ILogConfiguration
    {
        public string DbConnectionString { get; }
        public string RedisConnectionString { get; }
        public LogLevel MinimumLevel { get; }
        public string HashPepper { get; }
        public Config(IConfiguration configuration)
        {
            //connection strings and hashPepper are set in user secrets
            DbConnectionString = configuration["DbConnectionString"] ?? throw new Exception("Could not retrieve DB connection string.");
            RedisConnectionString = configuration["RedisConnectionString"] ?? throw new Exception("Could not retrieve Redis connection string.");
            MinimumLevel = Enum.Parse<LogLevel>(configuration["MinimumLogLevel"] ?? LogLevel.Info.ToString());
            HashPepper = configuration["HashPepper"] ?? throw new Exception("Could not retrieve pepper for Hashing.");
        }
    }
}
