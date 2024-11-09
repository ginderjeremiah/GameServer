using Game.Core.Infrastructure;
using Game.Infrastructure.Cache;
using Game.Infrastructure.Database;
using Game.Infrastructure.PubSub;

namespace Game.Infrastructure
{
    /// <summary>
    /// A concrete class to satisfy the interfaces required for configuring infrastructure services.
    /// </summary>
    public class InfrastructureOptions : IDatabaseOptions, ICacheOptions, IPubSubOptions
    {
        /// <inheritdoc/>
        public DatabaseSystem DatabaseSystem { get; set; }
        /// <inheritdoc/>
        public string? DbConnectionString { get; set; }

        /// <inheritdoc/>
        public CacheSystem CacheSystem { get; set; }
        /// <inheritdoc/>
        public string? CacheConnectionString { get; set; }

        /// <inheritdoc/>
        public PubSubSystem PubSubSystem { get; set; }
        /// <inheritdoc/>
        public string? PubSubConnectionString { get; set; }
    }
}
