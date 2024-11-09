using Game.Core.Infrastructure;

namespace Game.Infrastructure.Database
{
    /// <summary>
    /// Configuration options for a <see cref="GameContext"/>.
    /// </summary>
    public interface IDatabaseOptions
    {
        /// <summary>
        /// Determines which underlying database system to configure the <see cref="GameContext"/> for.
        /// </summary>
        public DatabaseSystem DatabaseSystem { get; }

        /// <summary>
        /// The connection string used by the <see cref="GameContext"/> to connect to a database.
        /// </summary>
        public string? DbConnectionString { get; }
    }
}
