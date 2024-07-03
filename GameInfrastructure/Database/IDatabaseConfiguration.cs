namespace GameInfrastructure.Database
{
    public interface IDatabaseConfiguration
    {
        public DatabaseSystem DatabaseSystem { get; }
        public string DbConnectionString { get; }
    }
}
