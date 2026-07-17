namespace Game.DataAccess.Mapping
{
    /// <summary>
    /// Raised when a player's owned reference (an item, item mod, or skill) cannot be resolved against the
    /// in-memory reference catalog while loading the player aggregate. Contiguity (id == index) plus
    /// retire-not-delete keep every owned slot resolvable forever (see docs/backend.md → Reference Data), so
    /// this should never fire in practice — a hit means a content-data mistake (a migration/seed error that
    /// actually removed a referenced id). The aggregate is failed loudly rather than silently dropping the
    /// player-owned row, and the message names the player, catalog, and missing id so the mistake is
    /// diagnosable straight from logs.
    /// </summary>
    internal sealed class OrphanedReferenceException(int playerId, string catalogName, int missingId, Exception innerException)
        : Exception(
            $"Player {playerId} owns a {catalogName} reference with Id {missingId} that no longer resolves against " +
            $"the {catalogName} catalog (a content-data mistake — a referenced id was removed). Player-owned " +
            "references must stay resolvable: top-level reference records are retired, never deleted.",
            innerException)
    {
        /// <summary>
        /// Resolves a player-owned reference against its catalog, rethrowing a catalog miss (a resolver's
        /// documented <see cref="ArgumentOutOfRangeException"/>) as a loud, diagnosable
        /// <see cref="OrphanedReferenceException"/> naming the player, catalog, and missing id. Any other
        /// failure propagates unwrapped so the orphaned-reference diagnosis stays truthful.
        /// </summary>
        public static T ResolveOrThrow<T>(Func<int, T> resolve, int referenceId, int playerId, string catalogName)
        {
            try
            {
                return resolve(referenceId);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                throw new OrphanedReferenceException(playerId, catalogName, referenceId, ex);
            }
        }
    }
}
