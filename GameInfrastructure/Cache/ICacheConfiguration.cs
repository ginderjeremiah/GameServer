namespace GameInfrastructure.Cache
{
    public interface ICacheConfiguration
    {
        public CacheSystem CacheSystem { get; }
        public string CacheConnectionString { get; }
    }
}
