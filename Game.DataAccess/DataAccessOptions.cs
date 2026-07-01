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

        /// <summary>
        /// When <see langword="true"/>, the API seeds the static reference-data content from the source-controlled
        /// export (<c>content/*.json</c>) on startup, after migrating, if the database has no content yet — giving
        /// a fresh dev / CI / recovery database a real content baseline. Idempotent (skips a populated database).
        /// Development always seeds; other environments opt in via this flag. Defaults to <see langword="false"/>,
        /// so production is never seeded this way unless explicitly enabled for a recovery.
        /// </summary>
        public bool SeedContentOnStartup { get; set; }
    }
}
