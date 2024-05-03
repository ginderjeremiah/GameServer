using GameLibrary.Database.Interfaces;

namespace GameServer
{
    public class Config : IDataConfiguration
    {
        public string DbConnectionString { get; }
        public string RedisConnectionString { get; }
        public string HashPepper { get; }
        public Config(IConfiguration configuration)
        {
            //connection strings and hashPepper are set in user secrets
            DbConnectionString = configuration["DbConnectionString"] ?? throw new Exception("Could not retrieve DB connection string.");
            RedisConnectionString = configuration["RedisConnectionString"] ?? throw new Exception("Could not retrieve Redis connection string.");
            HashPepper = configuration["HashPepper"] ?? throw new Exception("Could not retrieve pepper for Hashing.");
        }
    }
}
