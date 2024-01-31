using DataAccess;

namespace GameServer
{
    public class Config : IConfig
    {
        public string DbConnectionString { get; }
        public string RedisConnectionString { get; }
        public string HashPepper { get; }
        public Config(ConfigurationManager configuration)
        {
            //connection strings and hashPepper are set in user secrets
            DbConnectionString = configuration["DbConnectionString"] ?? throw new Exception("Could not retrieve DB connection string.");
            RedisConnectionString = configuration["RedisConnectionString"] ?? throw new Exception("Could not retrieve Redis connection string.");
            HashPepper = configuration["HashPepper"] ?? throw new Exception("Could not retrieve pepper for Hashing.");
        }

    }

    public interface IConfig : IDataConfiguration
    {
        public string HashPepper { get; }
    }
}
