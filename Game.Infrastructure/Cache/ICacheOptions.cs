using Game.Core.Infrastructure;

namespace Game.Infrastructure.Cache
{
    public interface ICacheOptions
    {
        public CacheSystem CacheSystem { get; }
        public string CacheConnectionString { get; }
    }
}
