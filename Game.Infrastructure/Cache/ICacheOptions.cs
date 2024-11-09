using Game.Core.Infrastructure;

namespace Game.Infrastructure.Cache
{
    /// <summary>
    /// Configuration options for an <see cref="ICacheService"/>.
    /// </summary>
    public interface ICacheOptions
    {
        /// <summary>
        /// Determines which underlying <see cref="ICacheService"/> implementation to use.
        /// </summary>
        public CacheSystem CacheSystem { get; }

        /// <summary>
        /// The connection string used to configure the <see cref="ICacheService"/>.
        /// </summary>
        public string? CacheConnectionString { get; }
    }
}
