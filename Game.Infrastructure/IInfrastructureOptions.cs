using Game.Core.Infrastructure;
using Game.Infrastructure.Cache;
using Game.Infrastructure.Database;
using Game.Infrastructure.PubSub;

namespace Game.Infrastructure
{
    public class InfrastructureOptions : IDatabaseOptions, ICacheOptions, IPubSubOptions
    {
        public DatabaseSystem DatabaseSystem { get; set; }
        public string DbConnectionString { get; set; }

        public CacheSystem CacheSystem { get; set; }
        public string CacheConnectionString { get; set; }

        public PubSubSystem PubSubSystem { get; set; }
        public string PubSubConnectionString { get; set; }
    }
}
