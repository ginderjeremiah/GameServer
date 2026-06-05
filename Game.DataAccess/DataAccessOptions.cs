using Game.Infrastructure;

namespace Game.DataAccess
{
    /// <summary>
    /// A concrete class for options used to configure data access services.
    /// </summary>
    public class DataAccessOptions : InfrastructureOptions
    {
        /// <summary>
        /// When <see langword="true"/>, the API applies any pending EF Core migrations on startup
        /// regardless of the hosting environment. This lets the Dockerized API (used by CI and
        /// end-to-end runs) converge an empty database to the current schema without relying on the
        /// Development-only tooling. Defaults to <see langword="false"/>.
        /// </summary>
        public bool MigrateOnStartup { get; set; }
    }
}
