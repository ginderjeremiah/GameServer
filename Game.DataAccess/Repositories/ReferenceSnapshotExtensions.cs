namespace Game.DataAccess.Repositories
{
    /// <summary>
    /// Index helpers for the zero-based-id reference snapshots (items, skills, item mods, enemies, zones,
    /// challenges). A static reference record's Id is its index into the cached list — contiguity is
    /// guaranteed structurally by retire-not-delete (see docs/backend.md → Reference Data) — so resolving one
    /// by id is an array index. Centralizing the bounds check keeps the <c>Lookup*</c> (null) and <c>Get*</c>
    /// (throw) shapes symmetric and makes a bad id (a stale player row, migration drift) fail meaningfully
    /// rather than throwing a bare <see cref="ArgumentOutOfRangeException"/> from the raw indexer.
    /// </summary>
    internal static class ReferenceSnapshotExtensions
    {
        /// <summary>
        /// Returns the record whose Id (index) is <paramref name="id"/>, or <c>null</c> when the id is out of
        /// range. This is the <c>Lookup*</c> shape, for callers that handle a missing record themselves.
        /// </summary>
        public static T? Lookup<T>(this IReadOnlyList<T> snapshot, int id) where T : class
        {
            return id < 0 || snapshot.Count <= id ? null : snapshot[id];
        }

        /// <summary>
        /// Returns the record whose Id (index) is <paramref name="id"/>, throwing a descriptive
        /// <see cref="ArgumentOutOfRangeException"/> naming the id and <paramref name="setName"/> when the id is
        /// out of range. This is the gameplay <c>Get*</c> shape, where a non-resolving id is a corrupt
        /// reference that should surface meaningfully rather than as an opaque indexer crash.
        /// </summary>
        public static T GetById<T>(this IReadOnlyList<T> snapshot, int id, string setName)
        {
            if (id < 0 || snapshot.Count <= id)
            {
                throw new ArgumentOutOfRangeException(nameof(id), id, $"No {setName} exists with Id {id}.");
            }

            return snapshot[id];
        }
    }
}
