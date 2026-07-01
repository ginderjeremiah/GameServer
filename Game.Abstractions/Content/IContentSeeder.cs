namespace Game.Abstractions.Content
{
    /// <summary>
    /// Seeds a freshly-migrated database with the static reference-data graph from the content export (spike
    /// #1390, decision 3). A dedicated bulk seeder — not a replay through the admin repositories — so it can
    /// write entities directly with their <em>explicit</em> ids (preserving the 0-based contiguity the caches
    /// assert), preserve <c>RetiredAt</c>, and satisfy forward references by inserting in dependency order.
    /// Coexists with the intrinsic <c>HasData</c> seeds already present after migration.
    /// </summary>
    public interface IContentSeeder
    {
        /// <summary>
        /// Writes the whole content graph in FK/dependency order when the database has no static content yet.
        /// Idempotent: a database that already holds content (an authored dev DB, or a second call) is left
        /// untouched. Returns <see langword="true"/> when it seeded, <see langword="false"/> when it skipped
        /// because content was already present. Does <em>not</em> reload the reference caches — the caller
        /// reloads them (at startup the eager cache load that follows migration picks the seed up).
        /// </summary>
        Task<bool> SeedAsync(ContentImport content, CancellationToken cancellationToken = default);
    }
}
