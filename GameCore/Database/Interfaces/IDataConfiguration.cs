namespace GameCore.Database.Interfaces
{
    public interface IDataConfiguration
    {
        public string DbConnectionString { get; }
        public string RedisConnectionString { get; }
    }
}
